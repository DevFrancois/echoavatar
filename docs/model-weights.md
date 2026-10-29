# Official EchoAvatar weights and download provenance

Checked **29 October 2026**. This records availability and metadata, not a local
inference result. See [the architecture/training study](echoavatar-research.md).

**Download follow-up:** all listed model folders, the separate EnCodec file,
Unity archive and paper are now present in this fork. Actual downloaded hashes
are checked in [assets-validation.json](assets-validation.json); see
[asset-archive.md](asset-archive.md) for LFS and Unity instructions. The original
metadata-only investigation below is distinct from this later file verification.

## Official locations

- [EchoAvatar checkpoint collection](https://huggingface.co/robinwitch/EchoAvatar/tree/69cb06bf4918f1c52cc276ca796bbff97c643b70/ckpts),
  revision `69cb06bf4918f1c52cc276ca796bbff97c643b70`.
- [Separate mHuBERT collection](https://huggingface.co/robinwitch/hf_transformer_mhubert_base_vp_en_es_fr_it3/tree/030c06e33a8b8feb094b0a92f023ddd80599793e),
  revision `030c06e33a8b8feb094b0a92f023ddd80599793e`.
- [EnCodec 24 kHz checkpoint](https://dl.fbaipublicfiles.com/encodec/v0/encodec_24khz-d7cc33bc.th),
  the download used by the bundled [EnCodec loader](../refers/encodec/encodec/model.py).

Both Hugging Face repositories were public and ungated when checked. Exact paths,
byte sizes, pinned download URLs and **server-reported** LFS SHA-256 values are in
[`echoavatar-weights-manifest.json`](echoavatar-weights-manifest.json). These remote
hashes alone are not local checksum verification. The subsequent
[downloaded-file report](assets-validation.json) records matching local checks.

| Component | Official location | Download size | Purpose |
| --- | --- | ---: | --- |
| `body_g` | [Complete folder](https://huggingface.co/robinwitch/EchoAvatar/tree/69cb06bf4918f1c52cc276ca796bbff97c643b70/ckpts/body_g) | 1,335,168,404 bytes | Speech-to-gesture body model; README gives no specific voice-timbre restriction. |
| `body_g_d` | [Complete folder](https://huggingface.co/robinwitch/EchoAvatar/tree/69cb06bf4918f1c52cc276ca796bbff97c643b70/ckpts/body_g_d) | 1,335,168,404 bytes | Speech gestures plus music/dance; README recommends a ZeroEGGS-like female voice for speech. |
| Lower/upper/hands RVQs | [Three checkpoints](https://huggingface.co/robinwitch/EchoAvatar/tree/69cb06bf4918f1c52cc276ca796bbff97c643b70/ckpts/body_rvqvae) | 399,158,969 bytes combined | Motion token encoders/decoders shared by the body variants. |
| Face | [Face checkpoint](https://huggingface.co/robinwitch/EchoAvatar/tree/69cb06bf4918f1c52cc276ca796bbff97c643b70/ckpts/face) | 382,222,347 bytes | `gpt_80000.pth`, loaded into `Audio2FaceGPT`. |
| mHuBERT | [Complete folder](https://huggingface.co/robinwitch/hf_transformer_mhubert_base_vp_en_es_fr_it3/tree/030c06e33a8b8feb094b0a92f023ddd80599793e) | 377,513,870 bytes including metadata | Face audio encoder. The actual weight filename is **`model.safetensors`** (377,510,632 bytes), not `pytorch_model.bin`. |
| EnCodec | [Weight file](https://dl.fbaipublicfiles.com/encodec/v0/encodec_24khz-d7cc33bc.th) | 93,171,529 bytes | Body audio tokenizer; auto-downloaded by its loader. Size comes from the earlier HEAD check. |
| Optional Unity project | [Unity ZIP](https://huggingface.co/robinwitch/EchoAvatar/resolve/69cb06bf4918f1c52cc276ca796bbff97c643b70/echoavatar_unity.zip) | 1,298,653,715 bytes | Reference deployment project, not neural weights. |

One body variant, the three RVQs, face, mHuBERT and EnCodec total
**2,587,235,119 bytes (approximately 2.59 decimal GB)**. Both body variants and the
shared files under EchoAvatar's `ckpts/**` total 3,451,718,124 bytes; that total
excludes the separately hosted mHuBERT, EnCodec and Unity ZIP. File sizes are not
runtime RAM/VRAM requirements.

The exact RVQ checkpoint basenames are lower `net_65000.pth`, upper
`net_54000.pth` and hands `net_60000.pth`. Their long parent-directory names are
preserved in the manifest and must match the loader paths.

## What the body checkpoints contain

Each body's `model.safetensors` is 1,313,245,824 bytes. The previous bounded header
inspection found 291 BF16 tensors and 656,606,592 parameters, including the
transformer, input embeddings and output head. No standalone LoRA/adapter keys
were found. The files are complete deployable body checkpoints; that observation
does not establish whether adapters were used and merged during training.

The freshly verified configs declare `Qwen2ForCausalLM`, 24 layers, width 896,
14 attention heads, 2 KV heads, vocabulary 166,690 and untied embeddings. The
configuration key is `dtype: "bfloat16"`. The complete folders include tokenizers,
added tokens, chat templates and generation/configuration files. Downloading
only the neural tensor file is insufficient for the supplied vLLM entry point.

No whole-body RVQ checkpoint, standalone adapter, or separately labeled optimizer
resume file was identified in this official inventory. The audit did not deserialize
the full `.pth` checkpoints. The local tokenizer trainer itself saves network
weights rather than a complete optimizer/scheduler resume state.

## Reproducible download commands

These commands are instructions for later use. **They were not executed** during
initial research task. Run them from the EchoAvatar repository root with the
Hugging Face CLI available.

For the speech-only body variant and shared components:

```sh
hf download robinwitch/EchoAvatar \
  --revision 69cb06bf4918f1c52cc276ca796bbff97c643b70 \
  --local-dir . \
  --include 'ckpts/body_g/**' 'ckpts/body_rvqvae/**' 'ckpts/face/**'

hf download robinwitch/hf_transformer_mhubert_base_vp_en_es_fr_it3 \
  --revision 030c06e33a8b8feb094b0a92f023ddd80599793e \
  --local-dir ./ckpts/hf_transformer_mhubert_base_vp_en_es_fr_it3
```

Replace `ckpts/body_g/**` with `ckpts/body_g_d/**` for the speech/dance variant.
The README's broader `--include 'ckpts/**'` downloads both body variants. The
EnCodec loader fetches its separate checkpoint when first instantiated; preserve
that file too if preparing a fully offline environment.

## Licensing and verification limits

The [source repository license](../LICENSE) is MIT. Neither audited Hugging Face
repository supplied an explicit model-license field or LICENSE/COPYING file.
mHuBERT's short README describes the SafeTensors conversion without licensing
terms. Do not assume the separate weights, training datasets or music inherit
the code's MIT license.

The initial audit used official API metadata, small configuration files and bounded
body safetensors headers. The later archive task downloaded the remaining assets
and verified full-file checksums, as linked above. It did not load checkpoints,
benchmark quality or prove runtime compatibility with a Mac.
