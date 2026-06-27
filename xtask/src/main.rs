fn main() -> anyhow::Result<()> {
    xtask::run(std::env::args().skip(1))
}
