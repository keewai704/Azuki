#[cfg(test)]
mod tests {
    use crate::{
        CandidateState, WindowRect,
        geometry::{WindowPoint, WindowSize},
    };
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
