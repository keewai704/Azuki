use std::path::{Path, PathBuf};

pub const DEFAULT_ZENZAI_MODEL_ID: &str = "zenz-v3.2-small-q5-k-m";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ZenzaiModel {
    pub id: &'static str,
    pub display_name: &'static str,
    pub repository: &'static str,
    pub filename: &'static str,
    pub url: &'static str,
    pub expected_size_bytes: u64,
    pub sha256: &'static str,
}

const MODELS: [ZenzaiModel; 4] = [
    ZenzaiModel {
        id: "zenz-v3.2-small-q5-k-m",
        display_name: "Zenz v3.2 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3.2-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3.2-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 73_871_936,
        sha256: "29c223d4c23327b80fd13ebb5ab2555057a46317997d5da391584ffbef0db673",
    },
    ZenzaiModel {
        id: "zenz-v3.1-small-q5-k-m",
        display_name: "Zenz v3.1 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3.1-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3.1-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 73_871_968,
        sha256: "4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c",
    },
    ZenzaiModel {
        id: "zenz-v3-small-q5-k-m",
        display_name: "Zenz v3 small (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v3-small-gguf",
        filename: "ggml-model-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v3-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf",
        expected_size_bytes: 72_298_816,
        sha256: "501f605d088f5b988791a00ae19ed46985ed7c48144f364b2f3f1f951c9b2083",
    },
    ZenzaiModel {
        id: "zenz-v2-q5-k-m",
        display_name: "Zenz v2 (Q5_K_M)",
        repository: "Miwa-Keita/zenz-v2-gguf",
        filename: "zenz-v2-Q5_K_M.gguf",
        url: "https://huggingface.co/Miwa-Keita/zenz-v2-gguf/resolve/main/zenz-v2-Q5_K_M.gguf",
        expected_size_bytes: 72_298_816,
        sha256: "22b8d8190bba8c9fec075ffb5b323b0f0d65c7c5f5ff82011799a0c3049d9662",
    },
];

pub fn available_models() -> &'static [ZenzaiModel] {
    &MODELS
}

pub fn resolve_model(id: &str) -> &'static ZenzaiModel {
    MODELS
        .iter()
        .find(|model| model.id == id)
        .unwrap_or_else(|| {
            MODELS
                .iter()
                .find(|model| model.id == DEFAULT_ZENZAI_MODEL_ID)
                .expect("default Zenzai model must be present")
        })
}

pub fn default_model_id() -> String {
    DEFAULT_ZENZAI_MODEL_ID.to_string()
}

pub fn model_path(config_root: &Path, model: &ZenzaiModel) -> PathBuf {
    config_root
        .join("models")
        .join(model.id)
        .join(model.filename)
}
