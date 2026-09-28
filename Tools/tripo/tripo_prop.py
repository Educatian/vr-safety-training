"""T2.3b: static prop adapter over the canonical Tripo client and credit ledger.

Defaults to a dry run. --generate submits one unrigged model using the existing
shared budget; it never changes that cap or uses a second ledger.
"""
import argparse
import importlib.util
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--name', default='tool-bag')
    parser.add_argument('--face-limit', type=int, default=8000)
    parser.add_argument('--generate', action='store_true')
    parser.add_argument('--client', type=Path, default=Path(
        'C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/tools/charpipe/tripo_char.py'))
    args = parser.parse_args()
    if not args.image.is_file():
        parser.error('Reference image is missing')
    if not 48 <= args.face_limit <= 8000:
        parser.error('Clutter prop face limit must be between 48 and 8000')
    if not args.client.is_file():
        parser.error('Canonical Tripo client is missing')
    spec = importlib.util.spec_from_file_location('canonical_tripo', args.client)
    client = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(client)
    spent = client.ledger_total()
    estimate = 30  # Standard-quality estimate from the existing client.
    print(f'Shared ledger: {spent:g}; cap: {client.BUDGET}; estimate: {estimate}')
    if args.generate and spent + estimate > client.BUDGET:
        parser.error('Existing shared cap blocks generation; no job submitted')
    forwarded = ['--image', str(args.image.resolve()), '--out', str(args.out.resolve()),
                 '--rig', 'none', '--quality', 'standard', '--api', 'v3',
                 '--face-limit', str(args.face_limit), '--game', 'vr-safety-training',
                 '--character', args.name]
    if not args.generate:
        forwarded.append('--dry-run')
    client.main(forwarded)


if __name__ == '__main__':
    main()
