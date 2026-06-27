use std::sync::{Arc, Mutex};

use crate::{CandidateState, WindowAction, WindowRect};
use shared::proto::{
    window_service_server::WindowService as WindowServiceProto, EmptyResponse, SetCandidateRequest,
    SetInputModeRequest, SetPositionRequest, SetSelectionRequest, UpdateCandidateWindowRequest,
    WindowPosition,
};
use tonic::{Request, Response, Status};

#[derive(Clone, Debug, Default)]
pub struct SharedCandidateState {
    state: Arc<Mutex<CandidateState>>,
}

impl SharedCandidateState {
    pub fn snapshot(&self) -> CandidateState {
        self.state.lock().expect("candidate state poisoned").clone()
    }

    fn apply(&self, action: WindowAction) {
        let mut state = self.state.lock().expect("candidate state poisoned");
        *state = state.clone().apply(action);
    }
}

#[derive(Clone, Debug)]
pub struct WindowService {
    state: SharedCandidateState,
}

impl WindowService {
    pub fn new(state: SharedCandidateState) -> Self {
        Self { state }
    }

    async fn send_action(&self, action: WindowAction) -> Result<Response<EmptyResponse>, Status> {
        self.state.apply(action);
        Ok(Response::new(EmptyResponse {}))
    }
}

#[tonic::async_trait]
impl WindowServiceProto for WindowService {
    async fn show_window(
        &self,
        _request: Request<EmptyResponse>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(WindowAction::Show).await
    }

    async fn hide_window(
        &self,
        _request: Request<EmptyResponse>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(WindowAction::Hide).await
    }

    async fn set_window_position(
        &self,
        request: Request<SetPositionRequest>,
    ) -> Result<Response<EmptyResponse>, Status> {
        let position = request
            .into_inner()
            .position
            .ok_or_else(|| Status::invalid_argument("position is required"))?;
        self.send_action(WindowAction::SetPosition(rect_from_proto(position)))
            .await
    }

    async fn set_candidate(
        &self,
        request: Request<SetCandidateRequest>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(WindowAction::SetCandidate(request.into_inner().candidates))
            .await
    }

    async fn set_selection(
        &self,
        request: Request<SetSelectionRequest>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(WindowAction::SetSelection(request.into_inner().index))
            .await
    }

    async fn set_input_mode(
        &self,
        request: Request<SetInputModeRequest>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(WindowAction::SetInputMode(request.into_inner().mode))
            .await
    }

    async fn update_candidate_window(
        &self,
        request: Request<UpdateCandidateWindowRequest>,
    ) -> Result<Response<EmptyResponse>, Status> {
        self.send_action(action_from_update_request(request.into_inner()))
            .await
    }
}

pub fn action_from_update_request(request: UpdateCandidateWindowRequest) -> WindowAction {
    WindowAction::UpdateCandidateWindow {
        visible: request.visible,
        position: request.position.map(rect_from_proto),
        candidates: request
            .candidates
            .map(|candidate_list| candidate_list.candidates),
        selected_index: request.selected_index,
        input_mode: request.input_mode,
        reading: request.reading,
        candidate_list_visible: request.candidate_list_visible,
        reading_vertical_adjustment: request.reading_vertical_adjustment,
    }
}

fn rect_from_proto(position: WindowPosition) -> WindowRect {
    WindowRect::new(position.top, position.left, position.bottom, position.right)
}

#[cfg(test)]
mod tests {
    use super::*;
    use shared::proto::{UpdateCandidateWindowRequest, WindowPosition};

    #[test]
    fn batched_proto_request_maps_optional_fields_to_action() {
        let action = action_from_update_request(UpdateCandidateWindowRequest {
            visible: Some(true),
            position: Some(WindowPosition {
                top: 1,
                left: 2,
                bottom: 3,
                right: 4,
            }),
            candidates: Some(shared::proto::CandidateList {
                candidates: vec!["候補".to_string()],
            }),
            selected_index: Some(0),
            input_mode: Some("あ".to_string()),
            reading: Some("よみ".to_string()),
            candidate_list_visible: Some(true),
            reading_vertical_adjustment: Some(4),
        });

        assert_eq!(
            action,
            WindowAction::UpdateCandidateWindow {
                visible: Some(true),
                position: Some(WindowRect::new(1, 2, 3, 4)),
                candidates: Some(vec!["候補".to_string()]),
                selected_index: Some(0),
                input_mode: Some("あ".to_string()),
                reading: Some("よみ".to_string()),
                candidate_list_visible: Some(true),
                reading_vertical_adjustment: Some(4),
            }
        );
    }
}
