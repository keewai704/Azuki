use anyhow::{bail, Context as _, Result};
use std::ffi::OsStr;
use std::path::{Path, PathBuf};
use std::process::Command;

use crate::args::{command_plan, usage};
use crate::plan::Step;

pub fn run<I, S>(args: I) -> Result<()>
where
    I: IntoIterator<Item = S>,
    S: Into<String>,
{
    let args = args.into_iter().map(Into::into).collect::<Vec<_>>();
    if args.is_empty() || matches!(args[0].as_str(), "-h" | "--help" | "help") {
        println!("{}", usage());
        return Ok(());
    }

    let plan = command_plan(args)?;
    let repo = repo_root();
    for step in plan.steps {
        run_step(&repo, step)?;
    }
    Ok(())
}

fn run_step(repo: &Path, step: Step) -> Result<()> {
    match step {
        Step::Format { check } => {
            let mut args = vec!["fmt"];
            if check {
                args.push("--check");
            }
            run_command(repo, "cargo", args)
        }
        Step::SyncVersion => run_command(repo, "node", ["scripts/sync-version.mjs"]),
        Step::BuildSwift => {
            let source = repo.join("llama_vulkan/llama.lib");
            anyhow::ensure!(
                source.is_file(),
                "Vulkan llama.lib was not found at {}",
                source.display()
            );
            std::fs::copy(&source, repo.join("server-swift/llama.lib"))
                .context("failed to prepare server-swift/llama.lib from Vulkan assets")?;
            run_command(
                &repo.join("server-swift"),
                "swift",
                [
                    "build",
                    "-c",
                    "release",
                    "-Xcc",
                    "-D_CRT_USE_C_COMPLEX_H",
                    "-Xcxx",
                    "-D_CRT_USE_C_COMPLEX_H",
                ],
            )?;
            std::fs::copy(
                repo.join("server-swift/.build/release/azookey-server.lib"),
                repo.join("azookey-server.lib"),
            )
            .context("failed to copy azookey-server.lib")?;
            Ok(())
        }
        Step::BuildX64(profile) => {
            let mut args = vec![
                "build",
                "-p",
                "azookey-server",
                "-p",
                "azookey-windows",
                "-p",
                "launcher",
            ];
            if let Some(profile_arg) = profile.cargo_arg() {
                args.push(profile_arg);
            }
            run_command(repo, "cargo", args)
        }
        Step::BuildX86(profile) => {
            let mut args = vec![
                "build",
                "-p",
                "azookey-windows",
                "--target=i686-pc-windows-msvc",
            ];
            if let Some(profile_arg) = profile.cargo_arg() {
                args.push(profile_arg);
            }
            run_command(repo, "cargo", args)
        }
        Step::BuildUi => {
            run_powershell_script(repo, "scripts/build-ui.ps1", std::iter::empty::<&str>())
        }
        Step::PostBuild(profile) => run_powershell_script(
            repo,
            "scripts/post-build.ps1",
            ["-Profile", profile.dir_name()],
        ),
        Step::BuildInstaller => run_powershell_script(
            repo,
            "scripts/build-installer.ps1",
            std::iter::empty::<&str>(),
        ),
    }
}

fn run_command<I, S>(cwd: &Path, program: &str, args: I) -> Result<()>
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    let args = args.into_iter().collect::<Vec<_>>();
    println!(
        "running: {} {}",
        program,
        args.iter()
            .map(|arg| arg.as_ref().to_string_lossy())
            .collect::<Vec<_>>()
            .join(" ")
    );
    let status = Command::new(program)
        .args(args.iter().map(AsRef::as_ref))
        .current_dir(cwd)
        .status()
        .with_context(|| format!("failed to start {program}"))?;
    if !status.success() {
        bail!("{program} failed with {status}");
    }
    Ok(())
}

fn run_powershell_script<I, S>(repo: &Path, script: &str, args: I) -> Result<()>
where
    I: IntoIterator<Item = S>,
    S: AsRef<OsStr>,
{
    let script_path = repo.join(script);
    let script_path = script_path
        .to_str()
        .with_context(|| format!("script path is not valid UTF-8: {}", script_path.display()))?;
    let script_args = args.into_iter().collect::<Vec<_>>();

    for shell in ["pwsh", "powershell"] {
        let status = Command::new(shell)
            .arg("-NoProfile")
            .arg("-ExecutionPolicy")
            .arg("Bypass")
            .arg("-File")
            .arg(script_path)
            .args(script_args.iter().map(AsRef::as_ref))
            .current_dir(repo)
            .status();

        match status {
            Ok(status) if status.success() => return Ok(()),
            Ok(status) => bail!("{shell} {script} failed with {status}"),
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => continue,
            Err(error) => return Err(error).with_context(|| format!("failed to start {shell}")),
        }
    }

    bail!("neither pwsh nor powershell was found")
}

fn repo_root() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .expect("xtask manifest should be inside the repository")
        .to_path_buf()
}
