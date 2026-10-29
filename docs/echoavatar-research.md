# EchoAvatar: architecture, custom training and reproduction

Research date: **29 October 2026**. Audited checkout:
`ecf2fff1a1cc3abb829d12b44db86c613c87ddcf`.
Git origin: <https://github.com/RobinWitch/EchoAvatar>.

**Later asset follow-up:** the fork now contains the downloaded checkpoints,
EnCodec, Unity archive/editable project and paper. Optional training archives were
also retrieved locally. See [asset-archive.md](asset-archive.md) for actual hashes,
locations and verification. The source-analysis conclusions below retain their
original audit scope; no inference/training result is implied by downloading.

These are local research notes, not an upstream release announcement. They combine
source inspection, the authors' paper and official checkpoint metadata. Independent
subagent passes checked training, architecture, checkpoint availability and our
Grok/Animates comparison. No EchoAvatar inference or training was run, and no full
neural checkpoint was downloaded during this investigation. Existing `server_test/`
work was left untouched and is outside this audit's claims.

Related notes:

- [Weights, exact downloads and provenance](model-weights.md).
- [Comparison with original Grok and recovered Animates](grok-animates-comparison.md).
- [Audited local source hashes](echoavatar-research-sources.json).
- [Official checkpoint inventory and remote LFS hashes](echoavatar-weights-manifest.json).

## Answers at a glance

| Question | Finding |
| --- | --- |
| Can we train with our own data? | **Yes, with qualifications.** The motion-tokenizer trainer exists. Custom body-generator and face-generator training require implementing missing training pipelines. |
| What data? | Motion for tokenizer training; synchronized audio plus body/finger motion for body generation; synchronized speech plus 52 ARKit facial coefficients for face generation. |
| Is the full architecture described? | The paper describes the method, and released source/configurations define an executable inference architecture. That is not a complete, reproducible training release. |
| Can we recreate it? | We can assemble the released architecture and weights. Re-training the entire paper system requires additional engineering, data and validation. Exact published results are not established by this audit. |
| Are weights available? | Yes, public official checkpoints exist for both body variants, three motion tokenizers and face, with separate audio encoders. Approximately **2.59 GB** for one body variant and its shared dependencies. |
| Does this immediately replace our Animates pipeline? | No. The representations, skeleton, history, audio encoders and runtime differ. A tested adapter and handoff are still needed. |

## Paper and official sources

**EchoAvatar: Real-time Generative Avatar Animation from Audio Streams**, Bohong
Chen, Yumeng Li, Yinglin Xu, Youyi Zheng, Yanlin Weng and Kun Zhou; SIGGRAPH 2026.

- [Paper landing page](https://arxiv.org/abs/2605.28272).
- [Version 1 full text and appendices](https://arxiv.org/html/2605.28272v1).
- [Paper PDF](https://arxiv.org/pdf/2605.28272v1).
- [ACM DOI](https://doi.org/10.1145/3799902.3811066).
- [Authors' project page](https://robinwitch.github.io/EchoAvatar-Page/).
- [Source at the audited revision](https://github.com/RobinWitch/EchoAvatar/tree/ecf2fff1a1cc3abb829d12b44db86c613c87ddcf).

The paper describes causal motion tokenization, three-stage LLM adaptation,
hierarchical token corruption/loss weighting, reference-motion control, GRPO and
DPO refinement. Appendix G covers facial generation. These descriptions support
reimplementation, but do not supply the missing trainers, private face captures
or complete preference/reward data. See sections 3, 4.2 and appendices D/G of the
[paper](https://arxiv.org/html/2605.28272v1).

## What can be trained today

| Component | Supplied implementation | Custom data required | Remaining work |
| --- | --- | --- | --- |
| Body motion tokenizers | `vq_train_bp_30fps_wandb_attn_poolmlp.py`, model and preprocessing source | Retargeted skeletal animation, including fingers, on the expected 88-joint rig | Prepare compatible data, correct the data-split/statistics issues below, train and evaluate reconstruction. |
| Audio-to-body generator | Inference pipeline and complete pretrained body checkpoints | Precisely synchronized speech/music and body/finger motion | Implement paired token preparation, sequence packing, losses, optimizer/training loop and evaluation. |
| Audio-to-face generator | Network and inference source, pretrained checkpoint | Synchronized speech and ordered 52-channel ARKit facial capture | Implement capture conversion, dataset, normalization, noise/timestep objective, training loop and evaluation. |
| Paper's refinement stages | Method description; no complete end-to-end trainer identified | Reward-training examples and/or preference judgments | Reconstruct the reward models, sampling, GRPO/DPO training and evaluation. |

The [README training section](../readme.md#training) provides tokenizer-training commands,
but empty body-generator and face-generator subsections. Its training-code and
benchmark-evaluation release checkboxes remain unchecked at the audited revision.
Finding neural-network classes or a forward method is not evidence of a complete
trainer.

### Required body data format

The reference is the `AX_female` rig. The ordered list in
[`process_zm_dataset.py`](../process_zm_dataset.py) contains **88 joints**. A valid
dataset must match bone order, hierarchy, rest offsets and coordinate conventions;
renaming another skeleton's bones is insufficient.

The processed representation has **531 values per frame**:

| Part | Values | Contents |
| --- | ---: | --- |
| Lower body | 117 | 19 joints × 6D rotation, plus root ΔX, ΔY and Z height |
| Upper body | 162 | 27 joints × 6D rotation |
| Hands | 252 | 42 joints × 6D rotation |
| Total | 531 | 88 local 6D rotations + 3 root features; Z-up |

The root increments are accumulated during playback; they are not absolute X/Y
positions. The training loader expects NumPy records containing
`{"file": clip_stem, "pose": array_of_shape_T_by_531}` in:

```text
datasets/zm/train_processed_30fps.npy
datasets/zm/valid_processed_30fps.npy
datasets/zm/mean_std_30fps.npy
```

The tokenizer loader only consumes motion. It does not require speech, transcripts,
phonemes, emotion labels or action labels. Foot-related losses are derived from
kinematics rather than a separate hand-labeled contact file. Body-generator
training adds synchronized audio; an MP3 alone cannot teach which movements to
produce. A mesh, a screenshot or ordinary video alone is also insufficient. Video
would first need motion reconstruction and quality control, especially for fingers.

### Existing datasets and custom capture

- [ZeroEGGS](https://github.com/ubisoft/ubisoft-laforge-ZeroEGGS): approximately
  135 minutes of speech/motion, 67 sequences, 19 styles and one female English
  speaker. Aligned WAV/BVH data make it relevant to speech gestures.
- [Motorica Dance Dataset](https://github.com/simonalexanderson/MotoricaDanceDataset):
  approximately six hours of dance motion across eight styles. Original capture
  is 120 fps; audio availability and finger quality need checking per sequence.
  Its research/noncommercial terms and music rights are separate from EchoAvatar's
  code license.
- The paper reports roughly one hour of private facial capture with speech and
  52 ARKit channels, using iPhone/Live Link Face. That capture dataset was not
  found in this release. [Paper, appendix G](https://arxiv.org/html/2605.28272v1).

For our own dataset, record synchronized clean audio, body/finger motion and, if
training the face, the ordered facial channels. Preserve timestamps and recording
boundaries, and split by recording/person/session rather than adjacent frames.
Our extracted authored Animates clips could support motion-only experiments after
retargeting. They do not become paired speech supervision merely because we have
audio elsewhere in the project.

### Preprocessing and training issues to fix before custom training

1. The documented pipeline retargets FBX using MotionBuilder, then converts BVH
   from Y-up to Z-up, selects the 88 joints, normalizes initial heading/translation
   and mirrors Motorica examples. The retargeting script has source-specific
   ZeroEGGS/Motorica mappings; custom skeletons need their own validated mapping.
2. After preparing/splitting BVH files, run `python process_zm_dataset.py` to make
   the processed arrays. This necessary step is omitted from the README's sequence.
   The current main routine selects 30 fps and dataset generation.
3. The processor assumes a 60 fps input stream and takes every second frame for
   30 fps. It is not a general resampler. Verify/export the source frame rate
   before using it, especially with raw 120 fps Motorica capture.
4. Statistics currently combine **train and validation** frames. When training a
   new tokenizer, compute its normalization from training data only to keep
   validation independent. When freezing a pretrained tokenizer, retain its
   shipped normalization instead; changing it would change the token contract.
5. The loader hardcodes 64-frame training crops, regardless of the nominal window
   argument. With its current `randint` call, training clips must be **longer than
   64 frames**, not merely equal to 64. Custom split lists must replace the
   bundled filenames.
6. The supplied `stats/zm_meta_info.npz` stores skeleton metadata. Changing the
   skeleton requires updating metadata, feature partitions, reconstruction and
   viewer conversion together; preprocessing reuses existing metadata.
7. `--resume-pth` reloads network weights. Saved tokenizer checkpoints are not a
   complete optimizer/scheduler/iteration resume state.

Evidence: [`process_zm_dataset.py`](../process_zm_dataset.py),
[`CustomDataset` and training loop](../vq_train_bp_30fps_wandb_attn_poolmlp.py),
[`prepare_bvh_format.py`](../tools/prepare_bvh_format.py),
[`motionbuilder_retarget_batch.py`](../tools/motionbuilder_retarget_batch.py).

Current tokenizer defaults include batch 256, 300,000 iterations, AdamW learning
rate `2e-4`, six 512-entry RVQ codebooks and commitment coefficient `0.5`.
Reconstruction, commitment, FK position/velocity/acceleration, root translation
and foot losses are implemented. Validation runs every 1,000 iterations. The
lower/upper/hands/whole commands are independent training jobs; listing four GPUs
in the README does not make four GPUs a mandatory minimum.

**Do not independently replace the tokenizer and expect the pretrained body LLM
to remain compatible.** A token ID refers to a learned codebook entry; retraining
that codebook changes its meaning. The most practical first personalization
experiment is to freeze the released tokenizers/audio encoder and adapt the body
generator to compatible paired data. Keep `stats/body_mean_std_30fps.npy` and
the reference skeleton contract with those frozen tokenizers. Likewise, an
existing face checkpoint depends on `stats/arkit_mean_std_30fps.npy`. Training
new tokenizers or changing normalization is a separate experiment requiring
coordinated generator adaptation and validation.

## Released inference architecture

The authoritative entry point for this audit is
[`5_streaming_vllm_unity_30fps_bp_attn4_encodec2_multirvq_nbc512_motionexample_withface_ik.py`](../scripts/5_streaming_vllm_unity_30fps_bp_attn4_encodec2_multirvq_nbc512_motionexample_withface_ik.py).

```text
24 kHz audio ──┬─ causal EnCodec, first 2 RVQ streams
              │    → discrete audio tokens + previous motion tokens
              │    → Qwen-derived body_g or body_g_d
              │    → lower / upper / hand token streams
              │    → 3 causal RVQ decoders → 531 motion features
              │    → denormalization, root integration, analytical hand IK
              │    → 88-joint body/finger animation
              │
              └─ resample to 16 kHz → mHuBERT
                   → autoregressive face transformer + flow head
                   → 52 facial coefficients → 47-channel Unity mapping

body + face + audio matched by chunk ID → Unity receiver
```

### Body and motion tokenizers

- EnCodec is the **causal 24 kHz** model. The entry point uses its first two
  1,024-entry codebooks: 75 time positions/second, or 150 flattened audio IDs/sec.
  The function name `wav2hubertcode` is misleading here: its body encoder is EnCodec.
- Official body configs specify `Qwen2ForCausalLM`, 24 layers, hidden width 896,
  14 query heads, 2 key/value heads, vocabulary 166,690 and untied embeddings.
  The prior bounded safetensors-header inspection found 291 BF16 tensors totaling
  656,606,592 parameters. These are full body checkpoints, not adapter-only files.
- Motion is partitioned into lower body, upper body and hands. Each tokenizer is
  `CausalAttentionPoolingMLP4_RVQVAE`: three encoder and three decoder attention
  blocks, width/code dimension 512, eight heads, MLP ratio four, two 2× temporal
  resampling stages, six separate 512-entry codebooks and lookbacks 15/7/3.
- This produces 7.5 latent time positions/sec. Six RVQ IDs × three body parts
  means 18 motion IDs per latent position, or 135 IDs/sec. The cyclic logits
  processor constrains generation to the correct codebook segment.
- Runtime uses the three part-specific tokenizers, not the separately trainable
  whole-body option. Their decoded feature partitions reassemble the 531 values.
- Although an L-BFGS hand solver is constructed, the variable is subsequently
  replaced by `realtime_hands_ik_solver_analytical`. The active path is analytical.

Sources: [entry point](../scripts/5_streaming_vllm_unity_30fps_bp_attn4_encodec2_multirvq_nbc512_motionexample_withface_ik.py),
[tokenizer implementation](../models/vq/casual_vqvae.py),
[EnCodec](../refers/encodec/encodec/model.py),
[body checkpoint metadata](echoavatar-weights-manifest.json).

### Face

- The instantiated network is `Audio2FaceGPT(diffusion_num_layers=3,
  hidden_size=64)`, with four transformer blocks and eight heads.
- mHuBERT supplies 768-dimensional audio features. A projection reduces them to
  the face network width. The active blocks use causal self-attention and additive
  audio conditioning; a commented cross-attention path is not active.
- A three-block adaptive-normalization MLP head generates 52 facial values with
  three sampling steps and classifier-free guidance scale two in this deployment.
- Runtime denormalizes using `stats/arkit_mean_std_30fps.npy`, remaps 52 channels
  to the Unity receiver's 47 channels, and smooths/interpolates to 60 fps.
- mHuBERT is bidirectional within its supplied audio window. The unused
  `make_attention_causal` helper does not make the running audio encoder causal.
  Streaming chunks therefore do not imply zero lookahead at each individual frame.
- The face runner normalizes each audio window and uses `63 * (16000 // 30)`
  samples, which is **33,579**, despite its inline comment saying 33,600.
  Preserve actual indexing when reproducing the released path.

Sources: [face model](../models/dit_flowmatching_mhubert_v3.py),
[streaming face runner](../scripts/5_streaming_vllm_unity_30fps_bp_attn4_encodec2_multirvq_nbc512_motionexample_withface_ik.py).

### History, startup and release differences

The released body worker uses a four-second audio window and retains 27 motion
latent positions, representing 3.6 seconds/486 IDs. Each update generates three
new latent positions/54 IDs, decodes a 120-frame context and emits its last
**12 frames (0.4 seconds)**. The face worker retains 52 facial frames for a
12-frame update and uses approximately 63 audio-frame positions.

At startup, the body context is filled with repeated code-index-32 patterns and
waits for four seconds of audio tokens. Facial history begins at zero in
**normalized coefficient space**. The sender discards early results until body
and face share a chunk ID. These are source observations, not a measured startup
experience here. No current displayed idle pose is captured by this entry point.

The paper describes 8-frame/266 ms updates and a width-256 face network; the
released entry point uses 12 frames and width 64. The paper's 56 face-history
frames likewise differ from this entry point's 52. Reproduce the released code
and checkpoint configuration first; do not silently mix paper hyperparameters
with those weights. [Paper](https://arxiv.org/html/2605.28272v1),
[entry point](../scripts/5_streaming_vllm_unity_30fps_bp_attn4_encodec2_multirvq_nbc512_motionexample_withface_ik.py).

Other model files in the checkout are not necessarily deployed components.
In particular, the presence of generic denoiser/motion-encoder files does not
make the active body generator a diffusion model: its running path generates
discrete motion IDs autoregressively.

## How we could recreate or adapt the system

1. **Establish inference first.** Pin this source revision and the checkpoint
   revisions in [the weights note](model-weights.md). Download one body variant,
   the three RVQs, face and both audio encoders. Retain the shipped normalization
   and skeleton metadata. Validate checkpoint loading, tensors and a short saved
   audio fixture before attempting customization.
2. **Make an offline adapter.** Export the 88-joint motion and facial channels
   without depending on the live Windows audio bridge. Verify root integration,
   axes, units, rotations and timing before connecting our viewer.
3. **Prepare custom paired data.** Retarget to the reference rig, validate audio
   alignment and fingers, create independent recording-level splits, and measure
   tokenizer reconstruction error on held-out recordings. Preserve released
   statistics for frozen tokenizers; use train-only statistics for a newly trained
   tokenizer.
4. **Implement missing training.** Initially freeze the existing tokenizers and
   audio encoder; add the body training pipeline and a separate face trainer.
   Treat this as custom adaptation, not an exact reproduction of the paper.
5. **Validate and then expand.** Compare audio response, repeatability, long-rollout
   stability, foot sliding, hand quality, held-out speakers and facial alignment.
   Only then consider tokenizer retraining, curriculum/refinement stages and
   current-pose/idle-to-speech handoff.

The architecture is sufficiently specified to build the released inference
system. Exact retraining remains unproven because complete trainers, processed
datasets, private face capture, preference data and some recipe details are
missing. There is also a paper commitment-weight inconsistency (section 4.2
versus appendix D); the local tokenizer trainer explicitly defaults to `0.5`.

The README targets Linux/NVIDIA inference and a Windows Unity client. It recommends
dual RTX 3090 or better for simultaneous body/face generation, and one GPU for
one branch. That is an **inference recommendation**, not a demonstrated training
minimum. Training/preprocessing contain direct CUDA calls; the stock setup is
not a verified Mac/MPS solution. The paper's reported two-H200 training run is
not a local benchmark or a hardware minimum.

## Boundaries of this investigation

At the initial source audit, the checkout contained code, reference assets and
normalization metadata, but no downloaded `ckpts/` collection or complete training
dataset was found. The later download follow-up is recorded above. `AX_female.7z`
is a character asset, not neural weights. No throughput, quality or memory claims
were measured locally. Public availability of a checkpoint does not establish
license equivalence with the repository's MIT code license; see the weights note.

EchoAvatar offers a clearer foundation for custom training than reverse-engineered
inference assets. Animates currently has stronger local numerical-execution
evidence in our project. Neither conclusion ranks visual quality. Switching to
EchoAvatar also does not automatically solve our Valentine's rig mapping or the
current-motion-to-generated-speech transition.
