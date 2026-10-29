#!/usr/bin/env bash
# EchoAvatar setup for the GPU server (conda version). Run from the EchoAvatar repo root,
# with conda already installed and initialised:
#   bash server_test/setup_server.sh
set -euo pipefail
cd "$(dirname "$0")/.."
eval "$(conda shell.bash hook)"

echo "=== 1/3  conda env 'echoavatar' (Python 3.13) + packages (several GB) ==="
conda env list | grep -q "^echoavatar " || conda create -n echoavatar python=3.13 -y
conda activate echoavatar
# requirements.txt pins transformers==4.55.0, but vllm 0.11.0 needs >=4.55.2
sed -i 's/^transformers==4.55.0$/transformers==4.55.2/' requirements.txt
pip install -r requirements.txt

echo "=== 2/3  Checkpoints (~4 GB) ==="
hf download robinwitch/EchoAvatar --local-dir . --include "ckpts/**"
hf download robinwitch/hf_transformer_mhubert_base_vp_en_es_fr_it3 \
   --local-dir ./ckpts/hf_transformer_mhubert_base_vp_en_es_fr_it3

echo "=== 3/3  Make the motion receiver address configurable (env vars) ==="
S=$(ls scripts/5_streaming_*.py)
sed -i "s/^MOTION_SERVER_HOST = .*/MOTION_SERVER_HOST = os.getenv('MOTION_SERVER_HOST', '127.0.0.1')/" "$S"
sed -i "s/^MOTION_SERVER_PORT = .*/MOTION_SERVER_PORT = int(os.getenv('MOTION_SERVER_PORT', '12347'))/" "$S"
grep -n "^MOTION_SERVER" "$S"

python -c "import torch, vllm; print('torch', torch.__version__, '| CUDA OK:', torch.cuda.is_available(), '| GPUs:', torch.cuda.device_count(), '| vllm', vllm.__version__)"
echo "=== Setup done ==="
