use crate::settings_model::{
    load_settings_snapshot, page_id_for_query, set_reading_adjustment, set_zenzai_backend_index,
    update_settings, zenzai_backend_items, SettingsSnapshot,
};
use windows_reactor::*;

pub fn run_settings() -> anyhow::Result<()> {
    windows_reactor::bootstrap()?;
    App::new()
        .title("Azookey Settings")
        .inner_size(1040.0, 720.0)
        .backdrop(Backdrop::Mica)
        .render(render_settings)?;
    Ok(())
}

fn render_settings(cx: &mut RenderCx) -> Element {
    cx.use_effect((), || {
        set_requested_theme(RequestedTheme::Dark);
        set_titlebar_height(true);
    });

    let (snapshot, set_snapshot) = cx.use_state(load_settings_snapshot());
    let (page, set_page) = cx.use_state(String::from("home"));
    let (search, set_search) = cx.use_state(String::new());

    let update_candidate_after_space = {
        let set_snapshot = set_snapshot.clone();
        move |enabled| {
            set_snapshot.call(update_settings(|config| {
                config.general.show_candidate_window_after_space = enabled;
            }))
        }
    };
    let update_live_reading = {
        let set_snapshot = set_snapshot.clone();
        move |enabled| {
            set_snapshot.call(update_settings(|config| {
                config.general.show_live_conversion_reading = enabled;
            }))
        }
    };
    let update_punctuation_commit = {
        let set_snapshot = set_snapshot.clone();
        move |enabled| {
            set_snapshot.call(update_settings(|config| {
                config.general.punctuation_commit = enabled;
            }))
        }
    };
    let update_zenzai_enabled = {
        let set_snapshot = set_snapshot.clone();
        move |enabled| {
            set_snapshot.call(update_settings(|config| {
                config.zenzai.enable = enabled;
            }))
        }
    };
    let update_server_log = {
        let set_snapshot = set_snapshot.clone();
        move |enabled| {
            set_snapshot.call(update_settings(|config| {
                config.debug.server_log_enabled = enabled;
            }))
        }
    };
    let update_reading_adjustment = {
        let set_snapshot = set_snapshot.clone();
        move |value: f64| {
            set_snapshot.call(update_settings(|config| {
                set_reading_adjustment(config, value.round() as i32);
            }))
        }
    };
    let update_zenzai_backend = {
        let set_snapshot = set_snapshot.clone();
        move |index| {
            set_snapshot.call(update_settings(|config| {
                set_zenzai_backend_index(config, index);
            }))
        }
    };

    let search_changed = {
        let set_search = set_search.clone();
        let set_page = set_page.clone();
        move |value: String| {
            if let Some(page_id) = page_id_for_query(&value) {
                set_page.call(page_id.to_string());
            }
            set_search.call(value);
        }
    };

    let title_bar = TitleBar::new("Azookey")
        .content(
            text_box(search.clone())
                .placeholder_text("設定の検索")
                .on_text_changed(search_changed)
                .width(520.0),
        )
        .tall(true);

    let current_page = settings_page(
        &page,
        &snapshot,
        update_candidate_after_space,
        update_live_reading,
        update_punctuation_commit,
        update_zenzai_enabled,
        update_server_log,
        update_reading_adjustment,
        update_zenzai_backend,
    );

    let navigation = NavigationView::new(nav_items(), current_page)
        .selected_tag(page.clone())
        .on_selection_changed(move |tag: String| {
            if !tag.is_empty() {
                set_page.call(tag);
            }
        })
        .pane_display_mode(NavigationViewPaneDisplayMode::Left)
        .pane_title("Azookey")
        .settings_visible(false)
        .back_button_visible(false);

    vstack((title_bar, navigation))
        .background(ThemeRef::SolidBackground)
        .into()
}

fn nav_items() -> Vec<NavViewItem> {
    vec![
        NavViewItem::new("ホーム").tag("home").icon(Symbol::Home),
        NavViewItem::new("一般")
            .tag("general")
            .icon(Symbol::Setting),
        NavViewItem::new("入力").tag("input").icon(Symbol::Keyboard),
        NavViewItem::new("候補").tag("candidate").icon(Symbol::List),
        NavViewItem::new("Zenzai").tag("zenzai").icon(Symbol::World),
        NavViewItem::new("デバッグ")
            .tag("debug")
            .icon(Symbol::Repair),
        NavViewItem::new("情報").tag("info").icon(Symbol::Help),
    ]
}

#[allow(clippy::too_many_arguments)]
fn settings_page(
    page: &str,
    snapshot: &SettingsSnapshot,
    update_candidate_after_space: impl Fn(bool) + Clone + 'static,
    update_live_reading: impl Fn(bool) + Clone + 'static,
    update_punctuation_commit: impl Fn(bool) + Clone + 'static,
    update_zenzai_enabled: impl Fn(bool) + Clone + 'static,
    update_server_log: impl Fn(bool) + Clone + 'static,
    update_reading_adjustment: impl Fn(f64) + Clone + 'static,
    update_zenzai_backend: impl Fn(i32) + Clone + 'static,
) -> Element {
    let content = match page {
        "general" => page_body(
            "一般",
            "句読点入力と基本動作",
            vec![setting_row(
                "句読点入力時に変換を確定",
                "「、」「。」などの入力で変換中の文字列を確定します。",
                toggle(snapshot.punctuation_commit, update_punctuation_commit),
            )],
        ),
        "input" => page_body(
            "入力",
            "日本語入力の動作",
            vec![
                value_row("入力方式", "ローマ字かな変換", "Azookey"),
                value_row(
                    "設定ファイル",
                    "現在の settings.json",
                    &snapshot.settings_path,
                ),
            ],
        ),
        "candidate" => page_body(
            "候補",
            "変換候補と読み表示",
            vec![
                setting_row(
                    "スペース変換後に候補ウィンドウを表示",
                    "スペースで変換したときに候補ポップアップを表示します。",
                    toggle(
                        snapshot.show_candidate_window_after_space,
                        update_candidate_after_space,
                    ),
                ),
                setting_row(
                    "ライブ変換中の読みを表示",
                    "ライブ変換中の読みを候補ポップアップ内に表示します。",
                    toggle(snapshot.show_live_conversion_reading, update_live_reading),
                ),
                setting_row(
                    "読み表示の高さ",
                    "読み表示の縦位置を調整します。",
                    reading_slider(snapshot, update_reading_adjustment),
                ),
            ],
        ),
        "zenzai" => page_body(
            "Zenzai",
            "Zenzai の有効化と backend",
            vec![
                setting_row(
                    "Zenzai を有効化",
                    "Zenzai による候補生成を使用します。",
                    toggle(snapshot.zenzai_enabled, update_zenzai_enabled),
                ),
                setting_row(
                    "Zenzai backend",
                    "使用する backend を選択します。",
                    ComboBox::new(zenzai_backend_items())
                        .selected_index(snapshot.zenzai_backend_index())
                        .on_selection_changed(update_zenzai_backend)
                        .width(180.0)
                        .into(),
                ),
            ],
        ),
        "debug" => page_body(
            "デバッグ",
            "ログ出力",
            vec![
                setting_row(
                    "サーバーログを有効化",
                    "変換サーバーのログ出力を有効にします。",
                    toggle(snapshot.server_log_enabled, update_server_log),
                ),
                value_row("設定ファイル", "保存先", &snapshot.settings_path),
            ],
        ),
        "info" => page_body(
            "情報",
            "Azookey の情報",
            vec![
                value_row("Azookey", "Windows 版", env!("CARGO_PKG_VERSION")),
                value_row("設定", "現在の状態", &snapshot.status),
            ],
        ),
        _ => home_page(snapshot),
    };

    scroll_viewer(content).into()
}

fn home_page(snapshot: &SettingsSnapshot) -> Element {
    let overview = grid((
        status_card("状態", &snapshot.status).grid_column(0),
        status_card("設定ファイル", &snapshot.settings_path).grid_column(1),
    ))
    .columns([GridLength::Star(1.0), GridLength::Star(1.0)])
    .column_spacing(16.0);

    let quick = grid((
        status_card(
            "候補",
            if snapshot.show_candidate_window_after_space {
                "スペース変換後に表示"
            } else {
                "必要なときに表示"
            },
        )
        .grid_column(0),
        status_card(
            "Zenzai",
            if snapshot.zenzai_enabled {
                "有効"
            } else {
                "無効"
            },
        )
        .grid_column(1),
    ))
    .columns([GridLength::Star(1.0), GridLength::Star(1.0)])
    .column_spacing(16.0);

    vstack((
        page_header("ホーム", "Azookey の状態と主要な設定"),
        overview,
        section_title("クイック アクセス"),
        quick,
    ))
    .spacing(18.0)
    .padding(Thickness::uniform(28.0))
    .into()
}

fn page_body(title: &str, description: &str, rows: Vec<Element>) -> Element {
    vstack((page_header(title, description), vstack(rows).spacing(10.0)))
        .spacing(18.0)
        .padding(Thickness::uniform(28.0))
        .into()
}

fn page_header(title: &str, description: &str) -> Element {
    vstack((
        text_block(title).font_size(28.0).bold(),
        text_block(description)
            .font_size(14.0)
            .foreground(ThemeRef::SecondaryText)
            .wrap(),
    ))
    .spacing(4.0)
    .into()
}

fn section_title(title: &str) -> Element {
    text_block(title).font_size(18.0).semibold().into()
}

fn setting_row(title: &str, description: &str, control: Element) -> Element {
    let label: Element = vstack((
        text_block(title).font_size(15.0).semibold(),
        text_block(description)
            .font_size(13.0)
            .foreground(ThemeRef::SecondaryText)
            .wrap(),
    ))
    .spacing(3.0)
    .into();

    let row = grid((
        label.grid_column(0),
        control
            .grid_column(1)
            .vertical_alignment(VerticalAlignment::Center),
    ))
    .columns([GridLength::Star(1.0), GridLength::Auto])
    .column_spacing(24.0);

    card(row.into())
}

fn value_row(title: &str, description: &str, value: &str) -> Element {
    setting_row(
        title,
        description,
        text_block(value)
            .font_size(13.0)
            .foreground(ThemeRef::SecondaryText)
            .wrap()
            .max_width(360.0)
            .into(),
    )
}

fn status_card(title: &str, value: &str) -> Element {
    card(
        vstack((
            text_block(title)
                .font_size(13.0)
                .foreground(ThemeRef::SecondaryText),
            text_block(value).font_size(16.0).semibold().wrap(),
        ))
        .spacing(6.0)
        .into(),
    )
}

fn card(content: Element) -> Element {
    border(content)
        .padding(Thickness::uniform(16.0))
        .background(ThemeRef::CardBackground)
        .border_brush(ThemeRef::CardStroke)
        .border_thickness(Thickness::uniform(1.0))
        .corner_radius(8.0)
        .into()
}

fn toggle(value: bool, on_toggled: impl Fn(bool) + 'static) -> Element {
    ToggleSwitch::new(value)
        .on_content("オン")
        .off_content("オフ")
        .on_toggled(on_toggled)
        .into()
}

fn reading_slider(
    snapshot: &SettingsSnapshot,
    on_value_changed: impl Fn(f64) + 'static,
) -> Element {
    hstack((
        Slider::new(snapshot.live_conversion_reading_vertical_adjustment as f64)
            .range(
                shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MIN as f64,
                shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MAX as f64,
            )
            .step(1.0)
            .on_value_changed(on_value_changed)
            .width(220.0),
        text_block(
            snapshot
                .live_conversion_reading_vertical_adjustment
                .to_string(),
        )
        .font_size(13.0)
        .foreground(ThemeRef::SecondaryText)
        .width(32.0),
    ))
    .spacing(10.0)
    .into()
}
