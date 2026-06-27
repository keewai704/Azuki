use std::thread;
use std::time::Duration;

use crate::{
    ipc::{SharedCandidateState, WindowService},
    window_host::{
        apply_candidate_window, configure_candidate_window, find_candidate_window,
        hide_candidate_window, CANDIDATE_WINDOW_TITLE,
    },
    CandidateState,
};
use shared::proto::window_service_server::WindowServiceServer;
use tonic::transport::Server;
use windows_reactor::*;

use crate::named_pipe::TonicNamedPipeServer;

pub fn run_ui() -> anyhow::Result<()> {
    let state = SharedCandidateState::default();
    start_window_service(state.clone());

    windows_reactor::bootstrap()?;
    App::new().run_custom(move |_| {
        let root_state = state.clone();
        let root: Box<dyn Component> =
            Box::new(move |_: &(), cx: &mut RenderCx| render_candidate_app(root_state.clone(), cx));
        let host = ReactorHost::new_with_window_options(
            CANDIDATE_WINDOW_TITLE,
            Some(windows_reactor::WindowSize {
                width: 320.0,
                height: 96.0,
            }),
            InnerConstraints {
                min_width: Some(240.0),
                min_height: Some(44.0),
                max_width: Some(480.0),
                max_height: Some(420.0),
            },
            root,
            |_| {},
        )?;

        if let Some(hwnd) = find_candidate_window() {
            configure_candidate_window(hwnd);
            hide_candidate_window(hwnd);
        }

        let _host = Box::leak(Box::new(host));
        Ok(())
    })?;
    Ok(())
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

fn render_candidate_app(shared: SharedCandidateState, cx: &mut RenderCx) -> Element {
    let (state, set_state) = cx.use_state(shared.snapshot());
    let timer_state = shared.clone();

    cx.use_effect_with_cleanup((), move || {
        let timer = DispatcherTimer::new(Duration::from_millis(30), move || {
            set_state.call(timer_state.snapshot());
        })
        .ok();

        timer.map(|timer| move || drop(timer))
    });

    let window_state = state.clone();
    cx.use_effect(window_state.clone(), move || {
        if let Some(hwnd) = find_candidate_window() {
            configure_candidate_window(hwnd);
            apply_candidate_window(hwnd, &window_state);
        }
    });

    set_requested_theme(RequestedTheme::Dark);
    render_candidate_state(&state)
}

fn render_candidate_state(state: &CandidateState) -> Element {
    let mut children = Vec::new();
    if !state.reading.is_empty() {
        children.push(
            text_block(state.reading.clone())
                .font_size(13.0)
                .foreground(ThemeRef::SecondaryText)
                .wrap()
                .into(),
        );
    }

    if state.candidate_list_visible {
        children.extend(
            state
                .candidates
                .iter()
                .enumerate()
                .map(|(index, candidate)| candidate_row(index, candidate, state.selected_index)),
        );
    }

    border(vstack(children).spacing(2.0))
        .padding(Thickness::uniform(8.0))
        .background(ThemeRef::CardBackground)
        .border_brush(ThemeRef::CardStroke)
        .border_thickness(Thickness::uniform(1.0))
        .corner_radius(8.0)
        .into()
}

fn candidate_row(index: usize, candidate: &str, selected_index: i32) -> Element {
    let is_selected = index as i32 == selected_index;
    let row = hstack((
        text_block(format!("{}", index + 1))
            .font_size(12.0)
            .foreground(if is_selected {
                ThemeRef::AccentText
            } else {
                ThemeRef::SecondaryText
            })
            .width(24.0),
        text_block(candidate.to_string())
            .font_size(17.0)
            .foreground(if is_selected {
                ThemeRef::AccentText
            } else {
                ThemeRef::PrimaryText
            })
            .wrap(),
    ))
    .spacing(8.0);

    let row = border(row)
        .padding(Thickness::xy(8.0, 4.0))
        .corner_radius(6.0);

    if is_selected {
        row.background(ThemeRef::Accent).into()
    } else {
        row.background(ThemeRef::SubtleFill).into()
    }
}
