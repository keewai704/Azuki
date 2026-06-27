use anyhow::{bail, Result};

use crate::plan::{CommandPlan, Profile, Step};

pub fn command_plan<I, S>(args: I) -> Result<CommandPlan>
where
    I: IntoIterator<Item = S>,
    S: Into<String>,
{
    let mut args = args.into_iter().map(Into::into);
    let Some(command) = args.next() else {
        bail!(usage());
    };
    let rest = args.collect::<Vec<_>>();

    match command.as_str() {
        "-h" | "--help" | "help" => Ok(CommandPlan { steps: Vec::new() }),
        "fmt" => {
            let check = parse_fmt_args(&rest)?;
            Ok(CommandPlan {
                steps: vec![Step::Format { check }],
            })
        }
        "sync-version" => no_args(&command, &rest).map(|()| CommandPlan {
            steps: vec![Step::SyncVersion],
        }),
        "build-swift" => no_args(&command, &rest).map(|()| CommandPlan {
            steps: vec![Step::BuildSwift],
        }),
        "build-x64" => {
            let profile = parse_profile(&rest)?;
            Ok(CommandPlan {
                steps: vec![Step::BuildX64(profile)],
            })
        }
        "build-x86" => {
            let profile = parse_profile(&rest)?;
            Ok(CommandPlan {
                steps: vec![Step::BuildX86(profile)],
            })
        }
        "build-winui" => no_args(&command, &rest).map(|()| CommandPlan {
            steps: vec![Step::BuildWinui],
        }),
        "post-build" => {
            let profile = parse_profile(&rest)?;
            Ok(CommandPlan {
                steps: vec![Step::PostBuild(profile)],
            })
        }
        "build-installer" => no_args(&command, &rest).map(|()| CommandPlan {
            steps: vec![Step::BuildInstaller],
        }),
        "build" => {
            let profile = parse_profile(&rest)?;
            Ok(CommandPlan {
                steps: vec![
                    Step::SyncVersion,
                    Step::BuildSwift,
                    Step::BuildX64(profile),
                    Step::BuildX86(profile),
                    Step::BuildWinui,
                    Step::PostBuild(profile),
                    Step::BuildInstaller,
                ],
            })
        }
        _ => bail!("unknown command: {command}\n\n{}", usage()),
    }
}

fn parse_fmt_args(args: &[String]) -> Result<bool> {
    match args {
        [] => Ok(false),
        [arg] if arg == "--check" => Ok(true),
        [arg] if arg == "-h" || arg == "--help" => bail!("usage: cargo xtask fmt [--check]"),
        _ => bail!("usage: cargo xtask fmt [--check]"),
    }
}

fn parse_profile(args: &[String]) -> Result<Profile> {
    match args {
        [] => Ok(Profile::Debug),
        [arg] if arg == "--debug" => Ok(Profile::Debug),
        [arg] if arg == "--release" => Ok(Profile::Release),
        _ => bail!("expected at most one profile flag: --debug or --release"),
    }
}

fn no_args(command: &str, args: &[String]) -> Result<()> {
    if args.is_empty() {
        Ok(())
    } else {
        bail!("{command} does not accept arguments")
    }
}

pub(crate) fn usage() -> &'static str {
    "usage:
  cargo xtask build [--debug|--release]
  cargo xtask fmt [--check]
  cargo xtask sync-version
  cargo xtask build-swift
  cargo xtask build-x64 [--debug|--release]
  cargo xtask build-x86 [--debug|--release]
  cargo xtask build-winui
  cargo xtask post-build [--debug|--release]
  cargo xtask build-installer"
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn build_release_runs_the_full_build_pipeline() {
        let plan = command_plan(["build", "--release"]).unwrap();

        assert_eq!(
            plan.steps,
            vec![
                Step::SyncVersion,
                Step::BuildSwift,
                Step::BuildX64(Profile::Release),
                Step::BuildX86(Profile::Release),
                Step::BuildWinui,
                Step::PostBuild(Profile::Release),
                Step::BuildInstaller,
            ]
        );
    }

    #[test]
    fn format_check_does_not_run_mutating_format() {
        let plan = command_plan(["fmt", "--check"]).unwrap();

        assert_eq!(plan.steps, vec![Step::Format { check: true }]);
    }

    #[test]
    fn x64_build_is_limited_to_shipped_rust_packages() {
        let plan = command_plan(["build-x64", "--release"]).unwrap();

        assert_eq!(plan.steps, vec![Step::BuildX64(Profile::Release)]);
    }
}
