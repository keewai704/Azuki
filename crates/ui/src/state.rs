pub use crate::geometry::WindowRect;

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum WindowAction {
    Show,
    Hide,
    SetPosition(WindowRect),
    SetCandidate(Vec<String>),
    SetSelection(i32),
    SetInputMode(String),
    UpdateCandidateWindow {
        visible: Option<bool>,
        position: Option<WindowRect>,
        candidates: Option<Vec<String>>,
        selected_index: Option<i32>,
        input_mode: Option<String>,
        reading: Option<String>,
        candidate_list_visible: Option<bool>,
        reading_vertical_adjustment: Option<i32>,
    },
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct CandidateState {
    pub visible: bool,
    pub position: Option<WindowRect>,
    pub candidates: Vec<String>,
    pub selected_index: i32,
    pub input_mode: String,
    pub reading: String,
    pub candidate_list_visible: bool,
    pub reading_vertical_adjustment: i32,
}

impl Default for CandidateState {
    fn default() -> Self {
        Self {
            visible: false,
            position: None,
            candidates: Vec::new(),
            selected_index: -1,
            input_mode: String::new(),
            reading: String::new(),
            candidate_list_visible: true,
            reading_vertical_adjustment:
                shared::LIVE_CONVERSION_READING_VERTICAL_ADJUSTMENT_DEFAULT,
        }
    }
}

impl CandidateState {
    pub fn apply(mut self, action: WindowAction) -> Self {
        match action {
            WindowAction::Show => {
                self.visible = true;
            }
            WindowAction::Hide => {
                self.visible = false;
                self.reading.clear();
            }
            WindowAction::SetPosition(position) => {
                self.position = Some(position);
            }
            WindowAction::SetCandidate(candidates) => {
                self.candidates = candidates;
                self.candidate_list_visible = true;
            }
            WindowAction::SetSelection(index) => {
                self.selected_index = index;
            }
            WindowAction::SetInputMode(input_mode) => {
                self.input_mode = input_mode;
            }
            WindowAction::UpdateCandidateWindow {
                visible,
                position,
                candidates,
                selected_index,
                input_mode,
                reading,
                candidate_list_visible,
                reading_vertical_adjustment,
            } => {
                if let Some(visible) = visible {
                    self.visible = visible;
                    if !visible {
                        self.reading.clear();
                    }
                }
                if let Some(position) = position {
                    self.position = Some(position);
                }
                if let Some(candidates) = candidates {
                    self.candidates = candidates;
                }
                if let Some(selected_index) = selected_index {
                    self.selected_index = selected_index;
                }
                if let Some(input_mode) = input_mode {
                    self.input_mode = input_mode;
                }
                if let Some(reading) = reading {
                    self.reading = reading;
                }
                if let Some(candidate_list_visible) = candidate_list_visible {
                    self.candidate_list_visible = candidate_list_visible;
                }
                if let Some(reading_vertical_adjustment) = reading_vertical_adjustment {
                    self.reading_vertical_adjustment = reading_vertical_adjustment;
                }
            }
        }

        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn update_candidate_window_applies_only_present_fields() {
        let initial = CandidateState::default()
            .apply(WindowAction::SetInputMode("あ".to_string()))
            .apply(WindowAction::SetCandidate(vec!["候補1".to_string()]))
            .apply(WindowAction::Show);

        let updated = initial.apply(WindowAction::UpdateCandidateWindow {
            visible: None,
            position: Some(WindowRect::new(10, 20, 30, 40)),
            candidates: None,
            selected_index: Some(0),
            input_mode: None,
            reading: Some("こうほ".to_string()),
            candidate_list_visible: Some(false),
            reading_vertical_adjustment: Some(4),
        });

        assert!(updated.visible);
        assert_eq!(updated.input_mode, "あ");
        assert_eq!(updated.candidates, vec!["候補1"]);
        assert_eq!(updated.position, Some(WindowRect::new(10, 20, 30, 40)));
        assert_eq!(updated.selected_index, 0);
        assert_eq!(updated.reading, "こうほ");
        assert!(!updated.candidate_list_visible);
        assert_eq!(updated.reading_vertical_adjustment, 4);
    }

    #[test]
    fn hide_clears_transient_reading_and_visibility() {
        let state = CandidateState::default()
            .apply(WindowAction::Show)
            .apply(WindowAction::UpdateCandidateWindow {
                visible: None,
                position: None,
                candidates: None,
                selected_index: None,
                input_mode: None,
                reading: Some("よみ".to_string()),
                candidate_list_visible: None,
                reading_vertical_adjustment: None,
            })
            .apply(WindowAction::Hide);

        assert!(!state.visible);
        assert!(state.reading.is_empty());
    }
}
