# Fork asset archive and additional-download audit

Updated 29 October 2026. This fork now preserves the downloaded inference assets,
the complete Unity release and its editable source project, the paper, and our
local research/server-test work. Large binary assets use Git LFS. The original
`EchoAvatar/` checkout was left unchanged.

## Included in this fork

| Location | Contents | Verification |
| --- | --- | --- |
| `ckpts/body_g/`, `ckpts/body_g_d/` | Both full body models, tokenizer files and configurations | Official pinned Hugging Face sizes, LFS SHA-256 or Git blob hashes |
| `ckpts/body_rvqvae/` | Lower-body, upper-body and hand motion tokenizers | Official pinned SHA-256 values |
| `ckpts/face/` | Face checkpoint | Official pinned SHA-256 |
| `ckpts/hf_transformer_mhubert_base_vp_en_es_fr_it3/` | Face audio encoder and configuration | Official pinned hashes |
| `ckpts/encodec/encodec_24khz-d7cc33bc.th` | Previously missing body audio encoder, 93,171,529 bytes | Expected size and official filename SHA-256 prefix; full local hash recorded |
| `echoavatar_unity.zip` | Complete original release, including Windows build and generated caches | Official SHA-256 and full ZIP CRC check |
| `echoavatar_unity/` | Byte-identical `Assets`, `Packages` and `ProjectSettings` extracted from that ZIP | All 260 source files checked against the archive |
| `docs/papers/echoavatar-2605.28272v1.pdf` | Paper and appendices, 10,088,086 bytes | PDF header, expected size and recorded local SHA-256 |
| `docs/` and `server_test/` | Research, comparisons, provenance and existing local test/setup scripts | Documentation links and Python/shell syntax checks |

The 31 official/additional download files total **5,231,145,324 bytes**; the
extracted editable Unity files add 603,868,848 bytes before Git LFS deduplication.
This is download/storage accounting, not RAM or VRAM requirements.

Evidence:

- [Actual downloaded-file validation](assets-validation.json).
- [Official remote inventory and pinned URLs](echoavatar-weights-manifest.json).
- [Unity source-file manifest](unity-project-manifest.json).
- [Architecture/training study](echoavatar-research.md).
- [Grok/Animates comparison](grok-animates-comparison.md).

## Using the archived files

Install Git LFS, then hydrate the large files after cloning:

```sh
git lfs install
git lfs pull
git lfs fsck
```

Open `echoavatar_unity/` through Unity Hub using **6000.0.58f1**. The project has
its original package manifest and lockfile. Unity regenerates its Library and
may retrieve registry packages. The full shipped Library, Windows build, logs and
user settings remain preserved inside the original ZIP; those generated folders
are not duplicated as editable source files.

EnCodec's upstream loader still uses the standard Torch Hub cache. We preserved
its checkpoint in the repository without changing inference code. To prepare
that cache without another model download, run the following from the repository
root in the inference environment:

```sh
python - <<'PY'
from pathlib import Path
import shutil
import torch

source = Path('ckpts/encodec/encodec_24khz-d7cc33bc.th')
cache = Path(torch.hub.get_dir()) / 'checkpoints'
cache.mkdir(parents=True, exist_ok=True)
shutil.copy2(source, cache / source.name)
PY
```

This archive step does not claim that inference, Unity compilation or interactive
playback was executed on this Mac. Package/runtime installation remains separate
from obtaining the source and weights.

## Optional training archives downloaded locally

The user requested these additional downloads and chose to keep training data
local. They are excluded from Git, including Git LFS. Their provenance and
verification are versioned in [training-downloads.json](training-downloads.json).

| Local directory | Files | Total bytes | Result |
| --- | --- | ---: | --- |
| `downloads/training-data/zeroeggs-original/` | `Zeggs_data.z01`, `.z02`, `.zip` | 2,769,699,515 | All three sizes and published SHA-256 values match |
| `downloads/training-data/zeroeggs-retarget/` | `fbx.zip` | 979,140,084 | All 135 ZIP members pass CRC checks |
| `downloads/training-data/motorica-retarget/` | `fbx.zip` | 1,578,992,545 | All 122 ZIP members pass CRC checks |
| **Total** | Five archives | **5,327,832,144** | Downloaded; not extracted |

Keep all three original ZeroEGGS parts together when extracting. Their hashes
were verified individually; a combined extraction was not performed. Dataset
terms differ from the code license; Motorica's terms specifically restrict
redistribution. The source and terms links are recorded in the manifest.

## What else exists, and what was not missing

The official EchoAvatar Hugging Face release at
`69cb06bf4918f1c52cc276ca796bbff97c643b70` contains the checkpoint folders, Unity
ZIP and repository attributes. Its GitHub Releases list was empty when checked.
No additional published whole-body RVQ, reward-model weights, optimizer resumes,
private face captures, preference dataset or complete body/face trainers were
found. These remain gaps in exact training reproduction, not failed downloads.

Already present in the source checkout were `tools/AX_female.7z` (35,308,954 bytes),
the reference voice WAV, skeleton/normalization data, Unity transform tables and
vendored EnCodec test audio. The reference character archive is preserved through
LFS; it was not separately extracted during this task.

EnCodec was the only missing weight used by the active deployment path. The
Wav2Vec2 helper and other unused model references do not require downloading
another base model for that path.

Potential later downloads:

- [Paper TeX/figure source](https://arxiv.org/src/2605.28272v1), 8,656,085 bytes;
  the PDF is already archived.
- [Motorica audio/data retrieval materials](https://github.com/simonalexanderson/MotoricaDanceDataset#obtaining-the-data).
  The retargeted FBX archive does not provide all aligned music. Raw music is not
  universally supplied, so the downloaded set is not a complete paired training
  dataset for every session.
- [Project demonstration videos](https://robinwitch.github.io/EchoAvatar-Page/).
  Eleven video references were found; direct HEAD requests returned HTTP 403, so
  download availability and sizes were not established. They are optional demos,
  not inference dependencies.

Download and validation are distinct from training readiness: retargeting,
alignment, data splits, normalization compatibility and the missing trainers
still need the work described in [the research note](echoavatar-research.md).
