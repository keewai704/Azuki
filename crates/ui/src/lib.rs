pub mod geometry;
pub mod ipc;
pub mod named_pipe;
pub mod settings_model;
pub mod state;
pub mod winui;

pub use geometry::{WindowPoint, WindowRect, WindowSize};
pub use state::{CandidateState, WindowAction};
