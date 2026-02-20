# Evaluators

`Evaluators` contains response curves and supporting abstractions used to map normalized signals (`0..1`) into utility scores (`0..1`).

Core types in this folder:
- `ICurve` and `Range` abstractions
- Built-in curves such as `LogisticCurve`, `PowerCurve`, `PiecewiseLinearCurve`, and `MonotoneCubicCurve`
- `Curves` helpers for common construction patterns
