use anyhow::{Context as _, Result};
use sha2::{Digest, Sha256};
use shared::{
    zenzai_models::{model_path, resolve_model, ZenzaiModel},
    AppConfig,
};
use std::{
    fs::{self, File},
    io::{Read, Write},
    path::{Path, PathBuf},
    time::Duration,
};

const MODEL_DOWNLOAD_CONNECT_TIMEOUT: Duration = Duration::from_secs(10);
const MODEL_DOWNLOAD_TOTAL_TIMEOUT: Duration = Duration::from_secs(180);

pub trait ModelDownloader {
    fn download(&self, url: &str, destination: &Path) -> Result<()>;
}

#[allow(dead_code)]
pub struct BlockingModelDownloader {
    connect_timeout: Duration,
    total_timeout: Duration,
}

impl Default for BlockingModelDownloader {
    fn default() -> Self {
        Self {
            connect_timeout: MODEL_DOWNLOAD_CONNECT_TIMEOUT,
            total_timeout: MODEL_DOWNLOAD_TOTAL_TIMEOUT,
        }
    }
}

impl BlockingModelDownloader {
    pub fn new() -> Self {
        Self::default()
    }

    #[cfg(test)]
    fn with_timeouts(connect_timeout: Duration, total_timeout: Duration) -> Self {
        Self {
            connect_timeout,
            total_timeout,
        }
    }

    fn client(&self) -> Result<reqwest::blocking::Client> {
        reqwest::blocking::Client::builder()
            .connect_timeout(self.connect_timeout)
            .timeout(self.total_timeout)
            .build()
            .context("failed to build model downloader")
    }
}

impl ModelDownloader for BlockingModelDownloader {
    fn download(&self, url: &str, destination: &Path) -> Result<()> {
        let client = self.client()?;
        let mut response = client
            .get(url)
            .send()
            .with_context(|| format!("failed to request {url}"))?
            .error_for_status()
            .with_context(|| format!("failed to download {url}"))?;
        let mut file = File::create(destination)
            .with_context(|| format!("failed to create {}", destination.display()))?;
        response
            .copy_to(&mut file)
            .with_context(|| format!("failed to write {}", destination.display()))?;
        file.flush()
            .with_context(|| format!("failed to flush {}", destination.display()))?;
        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ModelEnsureResult {
    pub path: Option<PathBuf>,
    pub error: Option<String>,
}

pub fn ensure_configured_model(
    config_root: &Path,
    config: &AppConfig,
    downloader: &dyn ModelDownloader,
) -> ModelEnsureResult {
    let model = resolve_model(&config.zenzai.model_id);
    match ensure_model_file(config_root, model, downloader) {
        Ok(path) => ModelEnsureResult {
            path: Some(path),
            error: None,
        },
        Err(error) => ModelEnsureResult {
            path: None,
            error: Some(error.to_string()),
        },
    }
}

pub fn ensure_model_file(
    config_root: &Path,
    model: &ZenzaiModel,
    downloader: &dyn ModelDownloader,
) -> Result<PathBuf> {
    let final_path = model_path(config_root, model);
    if verify_model_file(&final_path, model).unwrap_or(false) {
        return Ok(final_path);
    }

    if let Some(parent) = final_path.parent() {
        fs::create_dir_all(parent)
            .with_context(|| format!("failed to create {}", parent.display()))?;
    }

    let partial_path = partial_path(&final_path);
    let _ = fs::remove_file(&partial_path);
    downloader.download(model.url, &partial_path)?;
    anyhow::ensure!(
        verify_model_file(&partial_path, model)?,
        "downloaded model did not verify: {}",
        partial_path.display()
    );

    if final_path.exists() {
        fs::remove_file(&final_path)
            .with_context(|| format!("failed to remove {}", final_path.display()))?;
    }

    fs::rename(&partial_path, &final_path).with_context(|| {
        format!(
            "failed to promote {} to {}",
            partial_path.display(),
            final_path.display()
        )
    })?;
    Ok(final_path)
}

fn verify_model_file(path: &Path, model: &ZenzaiModel) -> Result<bool> {
    let metadata = match fs::metadata(path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(false),
        Err(error) => return Err(error.into()),
    };
    if metadata.len() != model.expected_size_bytes {
        return Ok(false);
    }
    Ok(sha256_hex_file(path)? == model.sha256)
}

fn sha256_hex_file(path: &Path) -> Result<String> {
    let mut file = File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 64 * 1024];
    loop {
        let read = file.read(&mut buffer)?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    Ok(format!("{:x}", hasher.finalize()))
}

fn partial_path(path: &Path) -> PathBuf {
    let extension = path
        .extension()
        .and_then(|value| value.to_str())
        .map(|value| format!("{value}.partial"))
        .unwrap_or_else(|| "partial".to_string());
    path.with_extension(extension)
}

#[cfg(test)]
mod tests {
    use super::*;
    use shared::{zenzai_models::ZenzaiModel, AppConfig};
    use std::{
        cell::Cell,
        fs,
        net::TcpListener,
        thread,
        time::{Duration, Instant},
    };

    const EXISTING_TEST_MODEL: ZenzaiModel = ZenzaiModel {
        id: "test-existing",
        display_name: "Test existing",
        repository: "example/test",
        filename: "existing.gguf",
        url: "https://example.test/existing.gguf",
        expected_size_bytes: 14,
        sha256: "41cdbe602a4a31645f9eda434ee4adee1a5620a46066f7a29ea587b56b904a43",
    };

    const DOWNLOADED_TEST_MODEL: ZenzaiModel = ZenzaiModel {
        id: "test-downloaded",
        display_name: "Test downloaded",
        repository: "example/test",
        filename: "downloaded.gguf",
        url: "https://example.test/downloaded.gguf",
        expected_size_bytes: 16,
        sha256: "6f1b9e8b969d1ea18bd8ba51a2ba697f55142b337f163df6a7daf850453dd161",
    };

    struct FakeDownloader {
        calls: Cell<usize>,
        bytes: Vec<u8>,
        error: Option<&'static str>,
    }

    impl FakeDownloader {
        fn ok(bytes: Vec<u8>) -> Self {
            Self {
                calls: Cell::new(0),
                bytes,
                error: None,
            }
        }

        fn err(message: &'static str) -> Self {
            Self {
                calls: Cell::new(0),
                bytes: Vec::new(),
                error: Some(message),
            }
        }
    }

    impl ModelDownloader for FakeDownloader {
        fn download(&self, _url: &str, destination: &std::path::Path) -> anyhow::Result<()> {
            self.calls.set(self.calls.get() + 1);
            if let Some(message) = self.error {
                anyhow::bail!(message);
            }
            fs::write(destination, &self.bytes)?;
            Ok(())
        }
    }

    #[test]
    fn existing_verified_model_is_reused_without_download() {
        let temp = tempfile::tempdir().unwrap();
        let bytes = b"existing model".to_vec();
        let path = shared::zenzai_models::model_path(temp.path(), &EXISTING_TEST_MODEL);
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        fs::write(&path, &bytes).unwrap();
        let downloader = FakeDownloader::ok(b"replacement".to_vec());

        let result = ensure_model_file(temp.path(), &EXISTING_TEST_MODEL, &downloader).unwrap();

        assert_eq!(result, path);
        assert_eq!(downloader.calls.get(), 0);
    }

    #[test]
    fn missing_model_downloads_to_partial_and_promotes_after_hash_match() {
        let temp = tempfile::tempdir().unwrap();
        let bytes = b"downloaded model".to_vec();
        let downloader = FakeDownloader::ok(bytes.clone());

        let result = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader).unwrap();

        assert_eq!(fs::read(&result).unwrap(), bytes);
        assert!(!result.with_extension("gguf.partial").exists());
        assert_eq!(downloader.calls.get(), 1);
    }

    #[test]
    fn failed_download_does_not_create_final_model_file() {
        let temp = tempfile::tempdir().unwrap();
        let downloader = FakeDownloader::err("network down");

        let error = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader)
            .expect_err("download failure should be returned");

        assert!(error.to_string().contains("network down"));
        assert!(!shared::zenzai_models::model_path(temp.path(), &DOWNLOADED_TEST_MODEL).exists());
    }

    #[test]
    fn blocking_downloader_times_out_when_server_stalls() {
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let url = format!("http://{}", listener.local_addr().unwrap());
        let server = thread::spawn(move || {
            if let Ok((_stream, _addr)) = listener.accept() {
                thread::sleep(Duration::from_millis(500));
            }
        });
        let temp = tempfile::tempdir().unwrap();
        let destination = temp.path().join("model.gguf.partial");
        let downloader = BlockingModelDownloader::with_timeouts(
            Duration::from_millis(50),
            Duration::from_millis(100),
        );

        let started_at = Instant::now();
        let error = downloader
            .download(&url, &destination)
            .expect_err("stalled response should time out");

        assert!(
            started_at.elapsed() < Duration::from_secs(5),
            "timeout took too long: {:?}",
            started_at.elapsed()
        );
        assert!(
            error.to_string().contains("failed to request")
                || error.to_string().contains("failed to download")
        );
        assert!(!destination.exists());
        server.join().unwrap();
    }

    #[test]
    fn downloaded_model_with_wrong_hash_is_not_promoted() {
        let temp = tempfile::tempdir().unwrap();
        let downloader = FakeDownloader::ok(b"wrong model bytes".to_vec());

        let error = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader)
            .expect_err("hash mismatch should fail");

        assert!(error
            .to_string()
            .contains("downloaded model did not verify"));
        assert!(!shared::zenzai_models::model_path(temp.path(), &DOWNLOADED_TEST_MODEL).exists());
    }

    #[test]
    fn corrupt_existing_final_model_is_replaced_by_verified_download() {
        let temp = tempfile::tempdir().unwrap();
        let final_path = shared::zenzai_models::model_path(temp.path(), &DOWNLOADED_TEST_MODEL);
        fs::create_dir_all(final_path.parent().unwrap()).unwrap();
        fs::write(&final_path, b"corrupt bytes").unwrap();
        let bytes = b"downloaded model".to_vec();
        let downloader = FakeDownloader::ok(bytes.clone());

        let result = ensure_model_file(temp.path(), &DOWNLOADED_TEST_MODEL, &downloader).unwrap();

        assert_eq!(result, final_path);
        assert_eq!(fs::read(&result).unwrap(), bytes);
        assert!(!result.with_extension("gguf.partial").exists());
        assert_eq!(downloader.calls.get(), 1);
    }

    #[test]
    fn ensure_configured_model_returns_error_without_path_when_download_fails() {
        let temp = tempfile::tempdir().unwrap();
        let config = AppConfig::default();
        let downloader = FakeDownloader::err("offline");

        let result = ensure_configured_model(temp.path(), &config, &downloader);

        assert!(result.path.is_none());
        assert!(result.error.unwrap().contains("offline"));
    }
}
