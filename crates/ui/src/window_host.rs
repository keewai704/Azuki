use crate::{
    geometry::{candidate_window_position, WindowPoint, WindowSize},
    CandidateState, WindowRect,
};
use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, POINT, RECT};
use windows::Win32::Graphics::Gdi::{
    GetMonitorInfoW, MonitorFromPoint, MONITORINFO, MONITOR_DEFAULTTONEAREST,
};
use windows::Win32::UI::WindowsAndMessaging::{
    FindWindowW, GetCursorPos, GetWindowLongPtrW, SetWindowLongPtrW, SetWindowPos, ShowWindow,
    GWL_EXSTYLE, HWND_TOPMOST, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOOWNERZORDER, SWP_NOSIZE,
    SWP_NOZORDER, SWP_SHOWWINDOW, SW_HIDE, WS_EX_APPWINDOW, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
    WS_EX_TOPMOST,
};

pub const CANDIDATE_WINDOW_TITLE: &str = "Azookey Candidate";

const CANDIDATE_WIDTH: i32 = 320;
const MIN_CANDIDATE_HEIGHT: i32 = 44;
const ROW_HEIGHT: i32 = 34;
const READING_HEIGHT: i32 = 30;
const WINDOW_VERTICAL_PADDING: i32 = 12;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CandidatePopupPlan {
    Hide,
    Show {
        origin: WindowPoint,
        size: WindowSize,
        used_cursor_fallback: bool,
    },
}

pub fn candidate_has_content(state: &CandidateState) -> bool {
    !state.reading.is_empty() || (state.candidate_list_visible && !state.candidates.is_empty())
}

pub fn candidate_window_size(state: &CandidateState) -> WindowSize {
    let visible_candidates = if state.candidate_list_visible {
        state.candidates.len().max(1) as i32
    } else {
        0
    };
    let reading_height = if state.reading.is_empty() {
        0
    } else {
        READING_HEIGHT
    };
    let height = (visible_candidates * ROW_HEIGHT + reading_height + WINDOW_VERTICAL_PADDING)
        .max(MIN_CANDIDATE_HEIGHT);

    WindowSize::new(CANDIDATE_WIDTH, height)
}

pub fn candidate_popup_plan(
    state: &CandidateState,
    size: WindowSize,
    work_area: RECT,
    cursor_fallback: Option<WindowPoint>,
) -> CandidatePopupPlan {
    if !state.visible || !candidate_has_content(state) {
        return CandidatePopupPlan::Hide;
    }

    if let Some(target_rect) = state.position {
        return CandidatePopupPlan::Show {
            origin: candidate_window_position(target_rect, size, work_area),
            size,
            used_cursor_fallback: false,
        };
    }

    if let Some(cursor) = cursor_fallback {
        eprintln!("[ui] candidate window position missing; falling back to cursor position");
        return CandidatePopupPlan::Show {
            origin: clamp_popup_to_work_area(cursor, size, work_area),
            size,
            used_cursor_fallback: true,
        };
    }

    CandidatePopupPlan::Hide
}

pub fn find_candidate_window() -> Option<HWND> {
    let title = wide_null(CANDIDATE_WINDOW_TITLE);
    let hwnd = unsafe { FindWindowW(None, PCWSTR(title.as_ptr())) };
    if hwnd.0 == 0 {
        None
    } else {
        Some(hwnd)
    }
}

pub fn configure_candidate_window(hwnd: HWND) {
    unsafe {
        let ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        let new_ex_style = (ex_style | WS_EX_NOACTIVATE.0 | WS_EX_TOOLWINDOW.0 | WS_EX_TOPMOST.0)
            & !WS_EX_APPWINDOW.0;
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new_ex_style as isize);
    }
}

pub fn apply_candidate_window(hwnd: HWND, state: &CandidateState) {
    let size = candidate_window_size(state);
    let cursor = cursor_position();
    let work_area = state
        .position
        .map(|rect| work_area_near_point(WindowPoint::new(rect.left, rect.bottom)))
        .or_else(|| cursor.map(work_area_near_point))
        .unwrap_or_else(default_work_area);

    match candidate_popup_plan(state, size, work_area, cursor) {
        CandidatePopupPlan::Hide => unsafe {
            ShowWindow(hwnd, SW_HIDE);
        },
        CandidatePopupPlan::Show { origin, size, .. } => unsafe {
            let _ = SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                origin.x,
                origin.y,
                size.width,
                size.height,
                SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW,
            );
        },
    }
}

pub fn hide_candidate_window(hwnd: HWND) {
    unsafe {
        ShowWindow(hwnd, SW_HIDE);
        let _ = SetWindowPos(
            hwnd,
            HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_NOOWNERZORDER | SWP_NOZORDER,
        );
    }
}

fn clamp_popup_to_work_area(origin: WindowPoint, size: WindowSize, work_area: RECT) -> WindowPoint {
    let x = clamp_start(origin.x, size.width, work_area.left, work_area.right);
    let y = clamp_start(origin.y, size.height, work_area.top, work_area.bottom);
    WindowPoint::new(x, y)
}

fn clamp_start(preferred: i32, length: i32, min: i32, max: i32) -> i32 {
    if max <= min || length >= max - min {
        return min;
    }

    preferred.clamp(min, max - length)
}

fn cursor_position() -> Option<WindowPoint> {
    let mut point = POINT::default();
    if unsafe { GetCursorPos(&mut point).as_bool() } {
        Some(WindowPoint::new(point.x, point.y))
    } else {
        None
    }
}

fn work_area_near_point(point: WindowPoint) -> RECT {
    unsafe {
        let monitor = MonitorFromPoint(
            POINT {
                x: point.x,
                y: point.y,
            },
            MONITOR_DEFAULTTONEAREST,
        );
        let mut info = MONITORINFO {
            cbSize: std::mem::size_of::<MONITORINFO>() as u32,
            ..MONITORINFO::default()
        };
        if GetMonitorInfoW(monitor, &mut info).as_bool() {
            info.rcWork
        } else {
            default_work_area()
        }
    }
}

fn default_work_area() -> RECT {
    RECT {
        left: 0,
        top: 0,
        right: 1920,
        bottom: 1080,
    }
}

fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use windows::Win32::Foundation::RECT;

    fn work_area() -> RECT {
        RECT {
            left: 0,
            top: 0,
            right: 800,
            bottom: 600,
        }
    }

    #[test]
    fn popup_plan_hides_when_candidate_state_is_not_visible() {
        let state = CandidateState::default();

        let plan = super::candidate_popup_plan(&state, WindowSize::new(320, 96), work_area(), None);

        assert_eq!(plan, super::CandidatePopupPlan::Hide);
    }

    #[test]
    fn popup_plan_positions_visible_candidates_without_activation() {
        let state = CandidateState {
            visible: true,
            position: Some(WindowRect::new(100, 120, 124, 260)),
            candidates: vec!["候補".to_string(), "公報".to_string()],
            selected_index: 1,
            candidate_list_visible: true,
            ..CandidateState::default()
        };

        let plan = super::candidate_popup_plan(&state, WindowSize::new(320, 96), work_area(), None);

        assert_eq!(
            plan,
            super::CandidatePopupPlan::Show {
                origin: WindowPoint::new(105, 130),
                size: WindowSize::new(320, 96),
                used_cursor_fallback: false,
            }
        );
    }

    #[test]
    fn popup_plan_uses_cursor_fallback_when_tsf_position_is_missing() {
        let state = CandidateState {
            visible: true,
            candidates: vec!["候補".to_string()],
            candidate_list_visible: true,
            ..CandidateState::default()
        };

        let plan = super::candidate_popup_plan(
            &state,
            WindowSize::new(320, 56),
            work_area(),
            Some(WindowPoint::new(400, 300)),
        );

        assert_eq!(
            plan,
            super::CandidatePopupPlan::Show {
                origin: WindowPoint::new(400, 300),
                size: WindowSize::new(320, 56),
                used_cursor_fallback: true,
            }
        );
    }
}
