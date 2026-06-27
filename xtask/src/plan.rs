#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Profile {
    Debug,
    Release,
}

impl Profile {
    pub(crate) fn dir_name(self) -> &'static str {
        match self {
            Self::Debug => "debug",
            Self::Release => "release",
        }
    }

    pub(crate) fn cargo_arg(self) -> Option<&'static str> {
        match self {
            Self::Debug => None,
            Self::Release => Some("--release"),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Step {
    Format { check: bool },
    SyncVersion,
    BuildSwift,
    BuildX64(Profile),
    BuildX86(Profile),
    BuildUi,
    PostBuild(Profile),
    BuildInstaller,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CommandPlan {
    pub steps: Vec<Step>,
}
