use std::thread;
use std::time::Duration;

use crate::named_pipe::TonicNamedPipeServer;
use crate::{
    ipc::{SharedCandidateState, WindowService},
    settings_model::{
        load_settings_snapshot, set_reading_adjustment, set_zenzai_backend_index,
        settings_sections, update_settings, zenzai_backend_items,
    },
};
use shared::proto::window_service_server::WindowServiceServer;
use tonic::transport::Server;
use windows_reactor::*;

pub fn run_ui() -> anyhow::Result<()> {
    let state = SharedCandidateState::default();
    start_window_service(state.clone());

    windows_reactor::bootstrap()?;
    App::new()
        .title("Azookey UI")
        .inner_size(360.0, 220.0)
        .render(render_ui_app(state))?;
    Ok(())
}

pub fn run_settings() -> anyhow::Result<()> {
    windows_reactor::bootstrap()?;
    App::new()
        .title("Azookey Settings")
        .inner_size(860.0, 640.0)
        .render(render_settings)?;
    Ok(())
}

fn render_ui_app(shared: SharedCandidateState) -> impl Fn(&mut RenderCx) -> Element {
    move |cx| {
        let (state, set_state) = cx.use_state(shared.snapshot());
        let timer_state = shared.clone();

        cx.use_effect_with_cleanup((), move || {
            let timer = DispatcherTimer::new(Duration::from_millis(50), move || {
                set_state.call(timer_state.snapshot());
            })
            .ok();

            timer.map(|timer| move || drop(timer))
        });

        render_ui_state(&state)
    }
}

fn start_window_service(state: SharedCandidateState) {
    thread::spawn(move || {
        let runtime = match tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
        {
            Ok(runtime) => runtime,
            Err(error) => {
                eprintln!("[ui] failed to start tokio runtime: {error}");
                return;
            }
        };

        runtime.block_on(async move {
            let result = Server::builder()
                .add_service(WindowServiceServer::new(WindowService::new(state)))
                .serve_with_incoming(TonicNamedPipeServer::new("azookey_ui"))
                .await;
            if let Err(error) = result {
                eprintln!("[ui] azookey_ui pipe server stopped: {error}");
            }
        });
    });
}

fn render_ui_state(state: &crate::CandidateState) -> Element {
    let candidates: Element = if state.candidates.is_empty() {
        text_block("候補はありません").font_size(16.0).into()
    } else {
        vstack(
            state
                .candidates
                .iter()
                .enumerate()
                .map(|(index, candidate)| {
                    let label = format!("{}  {}", index + 1, candidate);
                    if index as i32 == state.selected_index {
                        text_block(label).semibold().font_size(18.0).into()
                    } else {
                        text_block(label).font_size(18.0).into()
                    }
                })
                .collect::<Vec<Element>>(),
        )
        .spacing(4.0)
        .into()
    };

    vstack((
        text_block(format!("入力モード: {}", empty_label(&state.input_mode))).font_size(16.0),
        text_block(format!("読み: {}", empty_label(&state.reading))).font_size(16.0),
        candidates,
    ))
    .spacing(10.0)
    .padding(Thickness::uniform(16.0))
    .into()
}

fn render_settings(cx: &mut RenderCx) -> Element {
    let (snapshot, set_snapshot) = cx.use_state(load_settings_snapshot());

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

    let controls = vstack((
        ToggleSwitch::new(snapshot.show_candidate_window_after_space)
            .header("スペース変換後に候補ウィンドウを表示")
            .on_content("オン")
            .off_content("オフ")
            .on_toggled(update_candidate_after_space),
        ToggleSwitch::new(snapshot.show_live_conversion_reading)
            .header("ライブ変換中の読みを表示")
            .on_content("オン")
            .off_content("オフ")
            .on_toggled(update_live_reading),
        Slider::new(snapshot.live_conversion_reading_vertical_adjustment as f64)
            .range(
                shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MIN as f64,
                shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_MAX as f64,
            )
            .step(1.0)
            .header(format!(
                "読み表示の高さ: {}",
                snapshot.live_conversion_reading_vertical_adjustment
            ))
            .on_value_changed(update_reading_adjustment),
        ToggleSwitch::new(snapshot.punctuation_commit)
            .header("句読点入力時に変換を確定")
            .on_content("オン")
            .off_content("オフ")
            .on_toggled(update_punctuation_commit),
        ToggleSwitch::new(snapshot.zenzai_enabled)
            .header("Zenzai を有効化")
            .on_content("オン")
            .off_content("オフ")
            .on_toggled(update_zenzai_enabled),
        ComboBox::new(zenzai_backend_items())
            .header("Zenzai backend")
            .selected_index(snapshot.zenzai_backend_index())
            .on_selection_changed(update_zenzai_backend),
        ToggleSwitch::new(snapshot.server_log_enabled)
            .header("サーバーログを有効化")
            .on_content("オン")
            .off_content("オフ")
            .on_toggled(update_server_log),
    ))
    .spacing(12.0)
    .max_width(520.0);

    let sections = vstack(
        settings_sections()
            .iter()
            .map(|section| {
                vstack((
                    text_block(section.title).semibold().font_size(20.0),
                    text_block(section.description).font_size(14.0).wrap(),
                ))
                .spacing(4.0)
                .padding(Thickness::uniform(12.0))
                .into()
            })
            .collect::<Vec<Element>>(),
    )
    .spacing(8.0);

    scroll_viewer(
        vstack((
            text_block("Azookey Settings").font_size(28.0).bold(),
            text_block("Rust / windows-rs / WinUI 3").font_size(14.0),
            text_block(snapshot.status).font_size(14.0).wrap(),
            text_block(snapshot.settings_path).font_size(12.0).wrap(),
            controls,
            sections,
        ))
        .spacing(16.0)
        .padding(Thickness::uniform(24.0)),
    )
    .into()
}

fn empty_label(value: &str) -> &str {
    if value.is_empty() {
        "-"
    } else {
        value
    }
}
