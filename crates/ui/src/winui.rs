pub fn run_ui() -> anyhow::Result<()> {
    crate::candidate_ui::run_ui()
}

pub fn run_settings() -> anyhow::Result<()> {
    crate::settings_ui::run_settings()
}
