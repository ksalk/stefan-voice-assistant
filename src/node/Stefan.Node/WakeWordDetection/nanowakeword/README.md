# NanoWakeWord model files

The node copies these files to its output `models/` directory at build time.

| File | SHA-256 | Notes |
|---|---|---|
| `stefan_v0.3.onnx` | `16bc126ca9973201b05f2f3979414c3846d9530b576db81b689486e26b9376f0` | Current model. Threshold `0.9`, trigger level `2`, refractory `60`, step frames `4`. |
| `stefan_v0.2.onnx` | `b26aa39b6d5029f7e707638f5125a79aa8a2cead1b31ea7bf79728c21a7ea945` | Previous model, kept for rollback. |
| `embedding_model.onnx` | `70d164290c1d095d1d4ee149bc5e00543250a7316b59f31d056cff7bd3075c1f` | Required by `stefan_v0.3`. This is the openWakeWord v0.5.1 conversion. |
| `melspectrogram.onnx` | `ba2b0e0f8b7b875369a2c89cb13360ff53bac436f2895cced9f479fa65eb176f` | Same file as NanoWakeWord's bundled copy. |

NanoWakeWord's bundled `embedding_model.onnx` (`ba754db3…`) is a different
ONNX conversion. Do not use it with `stefan_v0.3`: the model was trained
with the v0.5.1 conversion, and it benchmarks better with it. The v0.2 model is
the opposite and scores slightly better with the bundled conversion.

The full model notes, training data, and evaluation are in the model repository
`/home/ksalk/dev/nanowakeword-custom-model-2/README.md`.
