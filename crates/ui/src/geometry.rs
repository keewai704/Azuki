use windows::Win32::Foundation::RECT;

const CANDIDATE_X_OFFSET: i32 = 15;
const CANDIDATE_Y_GAP: i32 = 6;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct WindowPoint {
    pub x: i32,
    pub y: i32,
}

impl WindowPoint {
    pub const fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct WindowSize {
    pub width: i32,
    pub height: i32,
}

impl WindowSize {
    pub const fn new(width: i32, height: i32) -> Self {
        Self { width, height }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct WindowRect {
    pub top: i32,
    pub left: i32,
    pub bottom: i32,
    pub right: i32,
}

impl WindowRect {
    pub const fn new(top: i32, left: i32, bottom: i32, right: i32) -> Self {
        Self {
            top,
            left,
            bottom,
            right,
        }
    }
}

pub fn candidate_window_position(
    target_rect: WindowRect,
    window_size: WindowSize,
    work_area: RECT,
) -> WindowPoint {
    let x = clamp_start(
        target_rect.left - CANDIDATE_X_OFFSET,
        window_size.width,
        work_area.left,
        work_area.right,
    );

    let below = target_rect.bottom + CANDIDATE_Y_GAP;
    let above = target_rect.top - window_size.height - CANDIDATE_Y_GAP;
    let y = if below + window_size.height <= work_area.bottom {
        below
    } else if above >= work_area.top {
        above
    } else {
        let below_space = work_area.bottom.saturating_sub(target_rect.bottom);
        let above_space = target_rect.top.saturating_sub(work_area.top);
        let preferred = if below_space >= above_space {
            below
        } else {
            above
        };
        clamp_start(
            preferred,
            window_size.height,
            work_area.top,
            work_area.bottom,
        )
    };

    WindowPoint::new(x, y)
}

fn clamp_start(preferred: i32, length: i32, min: i32, max: i32) -> i32 {
    if max <= min || length >= max - min {
        return min;
    }

    preferred.clamp(min, max - length)
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
    fn candidate_window_clamps_to_work_area() {
        let position = candidate_window_position(
            WindowRect::new(100, 760, 120, 780),
            WindowSize::new(240, 120),
            work_area(),
        );

        assert_eq!(position, WindowPoint::new(560, 126));
    }

}
