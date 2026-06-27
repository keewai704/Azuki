fn main() {
    println!("cargo:rerun-if-changed=res/ui.rc");
    println!("cargo:rerun-if-changed=res/azookey.ico");
    let _ = embed_resource::compile("res/ui.rc", embed_resource::NONE);

    windows_reactor_setup::as_framework_dependent();
}
