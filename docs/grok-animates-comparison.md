# EchoAvatar compared with our Grok and Animates research

Snapshot: **29 October 2026**. Read alongside [the EchoAvatar study](echoavatar-research.md)
and [official weights inventory](model-weights.md).

There are **three distinct systems** here. The ten recovered Animates networks
are not the eight original Grok 1.3.27 networks. Similar companion features or
similar model roles do not prove identical architectures, weights or rig contracts.

## Main comparison

| Topic | EchoAvatar: released inference | Original Grok 1.3.27 | Animates 1.1.7 / build 96: recovered system |
| --- | --- | --- | --- |
| Evidence available | Source, research paper, public checkpoint metadata; no inference run in this audit | Static native/Swift/Core ML wrapper analysis; original neural weights still missing | Ten recovered Core ML graphs/weights, native-library analysis, controlled execution and comparison evidence |
| Body-generation method | Qwen-derived autoregressive generation of discrete motion tokens | Continuous body/hand latent flow; default FireFlow midpoint sampler, optional Euler | Continuous body-latent velocity model; explicit Euler integration in the verified reconstruction |
| Components | One selected body LLM, three motion RVQs, face model, EnCodec and mHuBERT; two alternative body checkpoints | Eight referenced components: `ua_en`, `ob_mb`, `rw_mb`, `ob_pc`, `ob_dn`, `ob_dc`, `rw_dc`, `af_dn` | Ten: `apr`, `aen`, `bem`, `bpp`, `bdn`, `bdc`, `ben`, `fdn`, `ib2`, `ib5` |
| Body rig | 88 ordered joints on the AX_female reference | 71 joints: 23 body + 24 in each hand | 61 joints on the verified source/binding skeleton |
| Body features | 531: 88 × 6D local rotations + root ΔX/ΔY/Z height | 212 body features, plus 216 for each hand | 558: 9 root features + 61 × 6D rotations + 61 × 3 angular velocities |
| Hands/fingers | Separate hand RVQ with 42 joints, combined with body parts after decoding | Shared left/right hand-history encoder and hand decoder; separate wrist latents | Included in the body representation and decoder; no separate wrist network among the ten |
| Body audio | 24 kHz, causal EnCodec; first two codebook streams | 15 kHz; 15,000-sample window, 4,000-sample hop; integer audio codes | 15 kHz; 18,250-sample window, 6,000-sample hop; APR then AEN codes/embeddings |
| Face audio | Separate 16 kHz mHuBERT path | Uses the original encoded audio stream | Uses the newer encoded audio streams and separate face codebook processing |
| Generated update | Released script: 12 frames at 30 fps (0.4 s) | 8 frames at 30 fps (about 0.267 s) | 12 frames at 30 fps (0.4 s) |
| Body history | 27 latent time positions = 3.6 s = 486 discrete IDs | Two feature frames for body and each hand | Four 558-value feature frames, embedded by BEM and pooled by BPP |
| Face representation/history | 52 coefficients, 52 previous frames in released 12-frame runner; remapped to 47 receiver channels | 52 channels, two history frames; native remapping/clamp and procedural blink | 52 channels, four history frames; denormalization and clamp to [0,1] |
| Sampling/control | Body token sampling and motion examples; face three-step sampler with CFG | Default ten body steps; numeric body/face class labels, gaze and CFG | Controlled body/face defaults four Euler steps; body 83-way and face 97-way style vectors, root commands |
| Transition machinery | Token-history continuation; no demonstrated displayed-idle handoff in audited entry point | Native application context/playback contracts recovered in part | BEN context/endpoint projection; IB2/IB5 bridges; application handoff/feedback still not fully reconstructed |
| Public training support | Tokenizer trainer exists; body/face training pipelines missing | No recovered training pipeline or original trained weights | Inference graphs and weights recovered; no original training data or full training pipeline |
| Weight availability | Public official downloads; about 2.59 GB for one branch selection and dependencies | Missing; cannot execute the original learned models from disassembly alone | 157,326,377 bytes across ten graph and ten weight files, verified against extraction manifest |
| Runtime | Supplied Linux/NVIDIA PyTorch/vLLM server and Windows Unity bridge | Original iOS Core ML/native code | Core ML on this Mac; all ten pass CPU-only and automatic-selection smoke execution |
| Local validation | Source/metadata audit only | Static findings; some decoder shapes are inferred from callers | Controlled 100-chunk speech/silence/tone/repeat comparisons pass; native resume has stated constraints |
| Valentine compatibility | Requires a new retargeting/facial adapter and tested playback handoff | Original family intended for Grok companions; missing weights prevent a complete local reconstruction, and present-day backend availability was not tested here | Current generated playback supports Quinn/Nixie/Yuki; Valentine's generated-speech adapter remains unresolved |

EchoAvatar's package sizes include different serialization, model families and
audio dependencies. Comparing download bytes is not a parameter-normalized,
quality or speed benchmark. Numerical style IDs and similarly named audio
embeddings must not be transferred between these systems without evidence.

## Model roles in the recovered Animates system

| Model | Established role | Current controlled basic speech path? |
| --- | --- | --- |
| `apr` | Audio window → mel features | Yes |
| `aen` | Mel features → audio indices and embeddings | Yes |
| `bem` | Four body feature frames → history embeddings | Yes |
| `bpp` | History embeddings → prefix tokens | Yes |
| `bdn` | Predict body latent velocity during integration | Yes |
| `bdc` | Decode latent into 12 × 558 body/finger features | Yes |
| `fdn` | Predict facial velocity during integration | Yes |
| `ben` | Body features → latent; optional native starting-context and bridge-endpoint projection | Not in the controlled basic speech generator; native projection can use it |
| `ib2` | Start/end-conditioned latent bridge, length 18; verified 24-frame output bridge | Separate bridge path |
| `ib5` | Start/end-conditioned latent bridge, length 45; verified 60-frame output bridge | Separate bridge path |

The BDN output is a **velocity**, not a decoded-motion latent to feed directly
into BDC. The sampler integrates it first. The normal body latent shape is
`[1,9,32]`; BEN maps `[1,12,558]` back into that latent representation. Native
context projection can run BEM → BPP → BEN → BDC. IB2/IB5 have separate selection
and scheduling contracts; they are not established as mandatory initial-speech
steps.

The native reconstruction uses AEN indices with body/face codebook processing;
simply wiring its exposed `q_hubert`/`q_beats` arrays into every downstream call
would skip recovered preprocessing. Names alone do not identify training data.

## What is actually validated in our project

All ten Animates components passed structural checks and CPU/automatic prediction
tests. The full-recording reconstruction matched the original native library for
**100 chunks** across supplied speech, silence, tone and repeated speech, at
`rtol=1e-3, atol=1e-4` with quaternion-sign-aware checks. Python continuation and
native resume passed in that controlled scope. Native resume currently requires
both root-command hysteresis flags to be inactive.

Those checks validate the numerical generation loop. They do not establish
complete application rendering, startup-state selection, displayed-frame feedback,
all transitions or Valentine's retargeting. Consult the current
[project status](../../grok/codex/valentine-audio-motion/docs/STATUS.md),
[full-recording report](../../grok/codex/valentine-audio-motion/docs/full-motion-checkpoint.md)
and [unresolved register](../../grok/codex/valentine-audio-motion/docs/UNRESOLVED.md).

## Why neither model choice alone fixes startup

Our controlled Animates generator repeats the application's binding pose for
its initial history and uses zero face channels. It does not receive the viewer's
current animation. Separately, the viewer lacks the complete native handoff into
generated speech. These are distinct generation-context and playback problems.

The Animates research recovered a normal five-raw-frame body context that becomes
four model feature frames; the extra raw frame supplies velocity context. It
includes all source-joint rotations, fingers and root state, with separate face
history. Application queue selection, delay, optional projection and playback
transition logic matter. This is not equivalent to taking one screen pose or
always grabbing the last four rendered frames.

EchoAvatar's audited entry point starts from repeated code-index-32 history and
zero normalized face history, with four seconds of body-audio warmup. It carries
generated token history afterward but does not show acquisition of a currently
displayed idle pose. Changing model families would therefore not, by itself,
implement the missing transition. The detailed Hopper-backed findings and closure
checks remain in:

- [Speech startup/history/transition research](../../grok/codex/valentine-audio-motion/docs/speech-start-transition.md).
- [Startup fix backlog](../../grok/codex/valentine-audio-motion/docs/UNRESOLVED.md#speech-startup-fix-backlog).
- [Fresh Hopper verification captures](../../grok/codex/valentine-audio-motion/outputs/hopper-runtime/20261029-speech-start-verification/).

## What each system is useful for

EchoAvatar is the stronger starting point for **custom training work** because its
source, tokenizer trainer and public weights expose more of the learning system.
That still leaves body/face trainers to implement. The recovered Animates system
currently offers stronger **local execution and native-comparison evidence** and
continues to serve the existing reconstruction project.

There has been no shared visual-quality benchmark, no EchoAvatar speed measurement
on this Mac and no proof of direct tensor compatibility. A fair experiment would
use the same audio, retain each system's expected skeleton/normalization, then
compare audio response, motion quality and properly retargeted playback separately.

## Evidence map and maintenance

These links into `../../grok/` require the sibling research folder in our workspace;
they are not files distributed by upstream EchoAvatar. Keep this comparison dated
when updating it, because older checkpoint notes may contain superseded limits.

| Subject | Primary local source |
| --- | --- |
| Original Grok eight models, rig, sampler and static confidence levels | [Original motion-generator analysis](../../grok/claude/04_ondevice_motion_generation.md) and [model I/O evidence](../../grok/claude/evidence/motion-generator/model_io.json) |
| Newer graph shapes and 157 MB extraction | [Model comparison](../../grok/codex/valentine-audio-motion/docs/model-comparison.txt) |
| Hand representation | [Hand-motion findings](../../grok/codex/valentine-audio-motion/docs/hand-motion.txt) |
| Ten-model execution | [Core ML checkpoint](../../grok/codex/valentine-audio-motion/docs/coreml-checkpoint.txt) |
| Controlled full-recording parity | [Full-motion checkpoint](../../grok/codex/valentine-audio-motion/docs/full-motion-checkpoint.md) |
| Bridges and optional projection | [Bridge checkpoint](../../grok/codex/valentine-audio-motion/docs/bridge-motion-checkpoint.md), [enabled controls](../../grok/codex/valentine-audio-motion/docs/bridge-enabled-controls.md) |
| Current capabilities and open problems | [Status](../../grok/codex/valentine-audio-motion/docs/STATUS.md), [unresolved register](../../grok/codex/valentine-audio-motion/docs/UNRESOLVED.md) |

The historical `model-comparison.txt` and `hand-motion.txt` remain useful for
interface facts; their older closing statements about missing viewer integration
are superseded by the current status document. Original Grok decoder dimensions
marked inferred remain inferences, not observations of unavailable weights.
