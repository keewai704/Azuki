use shared::{
    normalize_zenzai_backend, AppConfig, LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MAX,
    LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MIN,
};

const ZENZAI_BACKENDS: [&str; 1] = ["vulkan"];

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SettingsPage {
    pub id: &'static str,
    pub title: &'static str,
    pub description: &'static str,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SettingsSection {
    pub id: &'static str,
    pub title: &'static str,
    pub description: &'static str,
}

pub fn settings_pages() -> &'static [SettingsPage] {
    &[
        SettingsPage {
            id: "home",
            title: "ホーム",
            description: "Azookey の状態と主要な設定",
        },
        SettingsPage {
            id: "general",
            title: "一般",
            description: "句読点入力と基本動作",
        },
        SettingsPage {
            id: "input",
            title: "入力",
            description: "日本語入力の動作",
        },
        SettingsPage {
            id: "candidate",
            title: "候補",
            description: "変換候補と読み表示",
        },
        SettingsPage {
            id: "zenzai",
            title: "Zenzai",
            description: "Zenzai の有効化と backend",
        },
        SettingsPage {
            id: "debug",
            title: "デバッグ",
            description: "ログ出力",
        },
        SettingsPage {
            id: "info",
            title: "情報",
            description: "Azookey の情報",
        },
    ]
}

pub fn settings_sections() -> &'static [SettingsSection] {
    &[
        SettingsSection {
            id: "general",
            title: "一般",
            description: "句読点入力と基本動作",
        },
        SettingsSection {
            id: "input",
            title: "入力",
            description: "日本語入力の動作",
        },
        SettingsSection {
            id: "candidate",
            title: "候補",
            description: "変換候補と読み表示",
        },
        SettingsSection {
            id: "zenzai",
            title: "Zenzai",
            description: "Zenzai の有効化と backend",
        },
        SettingsSection {
            id: "debug",
            title: "デバッグ",
            description: "ログ出力",
        },
        SettingsSection {
            id: "info",
            title: "情報",
            description: "Azookey の情報",
        },
    ]
}

pub fn page_title(id: &str) -> &'static str {
    settings_pages()
        .iter()
        .find(|page| page.id == id)
        .map(|page| page.title)
        .unwrap_or("ホーム")
}

pub fn page_search_items() -> Vec<String> {
    settings_pages()
        .iter()
        .flat_map(|page| [page.title, page.description])
        .map(str::to_string)
        .collect()
}

pub fn page_id_for_query(query: &str) -> Option<&'static str> {
    let query = query.trim().to_lowercase();
    if query.is_empty() {
        return None;
    }

    settings_pages()
        .iter()
        .find(|page| {
            page.title.to_lowercase().contains(&query)
                || page.description.to_lowercase().contains(&query)
        })
        .map(|page| page.id)
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SettingsSnapshot {
    pub show_candidate_window_after_space: bool,
    pub show_live_conversion_reading: bool,
    pub live_conversion_reading_vertical_adjustment: i32,
    pub punctuation_commit: bool,
    pub zenzai_enabled: bool,
    pub zenzai_backend: String,
    pub server_log_enabled: bool,
    pub settings_path: String,
    pub status: String,
}

impl SettingsSnapshot {
    pub fn from_config(config: &AppConfig, status: impl Into<String>) -> Self {
        Self {
            show_candidate_window_after_space: config.general.show_candidate_window_after_space,
            show_live_conversion_reading: config.general.show_live_conversion_reading,
            live_conversion_reading_vertical_adjustment: config
                .general
                .live_conversion_reading_vertical_adjustment,
            punctuation_commit: config.general.punctuation_commit,
            zenzai_enabled: config.zenzai.enable,
            zenzai_backend: normalize_zenzai_backend(&config.zenzai.backend),
            server_log_enabled: config.debug.server_log_enabled,
            settings_path: settings_path_label(),
            status: status.into(),
        }
    }

    pub fn zenzai_backend_index(&self) -> i32 {
        ZENZAI_BACKENDS
            .iter()
            .position(|backend| *backend == self.zenzai_backend)
            .unwrap_or(0) as i32
    }
}

pub fn zenzai_backend_items() -> Vec<String> {
    ZENZAI_BACKENDS
        .iter()
        .map(|backend| (*backend).to_string())
        .collect()
}

pub fn load_settings_snapshot() -> SettingsSnapshot {
    match AppConfig::new_with_recovery() {
        Ok(result) => {
            let mut status = "設定を読み込みました".to_string();
            if let Some(recovery) = result.recovery {
                status = format!(
                    "破損した設定を退避して既定値を作成しました: {}",
                    recovery.backup_path.display()
                );
            }
            if let Some(error) = result.rewrite_error {
                status = format!("設定の再保存に失敗しました: {error}");
            }
            SettingsSnapshot::from_config(&result.config, status)
        }
        Err(error) => SettingsSnapshot::from_config(
            &AppConfig::default(),
            format!("設定を読み込めませんでした: {error}"),
        ),
    }
}

pub fn update_settings(mutator: impl FnOnce(&mut AppConfig)) -> SettingsSnapshot {
    let mut config = match AppConfig::read() {
        Ok(config) => config,
        Err(error) => {
            return SettingsSnapshot::from_config(
                &AppConfig::default(),
                format!("設定を読み込めませんでした: {error}"),
            );
        }
    };

    mutator(&mut config);

    match config.write() {
        Ok(()) => SettingsSnapshot::from_config(&config, "設定を保存しました"),
        Err(error) => {
            SettingsSnapshot::from_config(&config, format!("設定を保存できませんでした: {error}"))
        }
    }
}

pub fn set_reading_adjustment(config: &mut AppConfig, value: i32) {
    config.general.live_conversion_reading_vertical_adjustment = value.clamp(
        LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MIN,
        LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MAX,
    );
}

pub fn set_zenzai_backend_index(config: &mut AppConfig, index: i32) {
    let backend = ZENZAI_BACKENDS
        .get(index.max(0) as usize)
        .unwrap_or(&"vulkan");
    config.zenzai.backend = (*backend).to_string();
}

fn settings_path_label() -> String {
    AppConfig::settings_path()
        .map(|path| path.display().to_string())
        .unwrap_or_else(|error| format!("settings.json の場所を取得できません: {error}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn settings_sections_cover_expected_pages() {
        let titles = settings_sections()
            .iter()
            .map(|section| section.title)
            .collect::<Vec<_>>();

        assert_eq!(
            titles,
            vec!["一般", "入力", "候補", "Zenzai", "デバッグ", "情報"]
        );
    }

    #[test]
    fn settings_pages_are_japanese_powertoys_style_pages() {
        assert_eq!(
            settings_pages()
                .iter()
                .map(|page| page.title)
                .collect::<Vec<_>>(),
            vec![
                "ホーム",
                "一般",
                "入力",
                "候補",
                "Zenzai",
                "デバッグ",
                "情報",
            ]
        );

        let all_text = settings_pages()
            .iter()
            .flat_map(|page| [page.title, page.description])
            .collect::<Vec<_>>()
            .join("\n");

        assert!(!all_text.contains("Rust"));
        assert!(!all_text.contains("windows-rs"));
        assert!(!all_text.contains("WinUI 3"));
        assert!(!all_text.contains("繝"));
        assert!(!all_text.contains("縺"));
    }

    #[test]
    fn page_query_selects_matching_page() {
        assert_eq!(page_id_for_query("候補"), Some("candidate"));
        assert_eq!(page_id_for_query("backend"), Some("zenzai"));
        assert_eq!(page_id_for_query(""), None);
    }

    #[test]
    fn settings_snapshot_reflects_config_values() {
        let mut config = shared::AppConfig::default();
        config.general.show_candidate_window_after_space = true;
        config.general.show_live_conversion_reading = false;
        config.general.live_conversion_reading_vertical_adjustment = -3;
        config.general.punctuation_commit = true;
        config.zenzai.enable = true;
        config.zenzai.backend = "vulkan".to_string();
        config.debug.server_log_enabled = true;

        let snapshot = SettingsSnapshot::from_config(&config, "loaded");

        assert!(snapshot.show_candidate_window_after_space);
        assert!(!snapshot.show_live_conversion_reading);
        assert_eq!(snapshot.live_conversion_reading_vertical_adjustment, -3);
        assert!(snapshot.punctuation_commit);
        assert!(snapshot.zenzai_enabled);
        assert_eq!(snapshot.zenzai_backend, "vulkan");
        assert_eq!(snapshot.zenzai_backend_index(), 0);
        assert!(snapshot.server_log_enabled);
        assert_eq!(snapshot.status, "loaded");
    }

    #[test]
    fn set_reading_adjustment_clamps_to_supported_range() {
        let mut config = shared::AppConfig::default();

        set_reading_adjustment(&mut config, 99);
        assert_eq!(
            config.general.live_conversion_reading_vertical_adjustment,
            shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MAX
        );

        set_reading_adjustment(&mut config, -99);
        assert_eq!(
            config.general.live_conversion_reading_vertical_adjustment,
            shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MIN
        );
    }

    #[test]
    fn zenzai_backend_choices_only_include_vulkan() {
        assert_eq!(zenzai_backend_items(), vec!["vulkan"]);

        let mut config = shared::AppConfig::default();
        config.zenzai.backend = "cpu".to_string();

        set_zenzai_backend_index(&mut config, 99);

        assert_eq!(config.zenzai.backend, "vulkan");
    }

    #[test]
    fn ui_binaries_embed_azookey_icon_resource() {
        let manifest_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR"));

        assert!(manifest_dir.join("res/azookey.ico").is_file());
        assert!(manifest_dir.join("res/ui.rc").is_file());
    }
}
