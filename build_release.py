"""Repackage the released patch files and compile the Windows installer."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description='검증된 패치 파일로 자동·수동 배포본을 만듭니다.')
    parser.add_argument('--payload-root', type=Path, required=True,
                        help='수동판의 패치파일 폴더 (Guestbook_Data의 상위 폴더)')
    parser.add_argument('--output', type=Path, required=True, help='존재하지 않는 새 출력 폴더')
    parser.add_argument('--csc', type=Path, default=Path(os.environ.get('WINDIR', 'C:/Windows')) /
                        'Microsoft.NET/Framework64/v4.0.30319/csc.exe')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    manifest = json.loads((root / 'distribution/manifest.json').read_text(encoding='utf-8'))
    payload_root = args.payload_root.resolve()
    out = args.output.resolve()
    if out.exists():
        parser.error('출력 폴더가 이미 존재합니다. 새 폴더를 지정하세요.')
    if not args.csc.is_file():
        parser.error('Windows .NET Framework C# 컴파일러를 찾지 못했습니다.')
    for entry in manifest['files']:
        target = (payload_root / entry['path']).resolve()
        if not target.is_relative_to(payload_root) or not target.is_file():
            parser.error('패치 파일 누락 또는 경로 오류: ' + entry['path'])
        if sha(target) != entry['patched_sha256']:
            parser.error('패치 파일 체크섬 불일치: ' + entry['path'])
    auto = out / 'automatic'
    manual = out / 'manual'
    auto.mkdir(parents=True)
    manual.mkdir()
    payload = out / 'payload.zip'
    with zipfile.ZipFile(payload, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for entry in manifest['files']:
            rel = entry['path']
            archive.write(payload_root / rel, rel)
            dest = manual / '패치파일' / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(payload_root / rel, dest)
    exe = auto / '이계의 숙박부 한국어 패치 v1.0.0.exe'
    source = root / 'installer'
    subprocess.run([
        str(args.csc), '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/debug-',
        '/out:' + str(exe), '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
        '/r:System.IO.Compression.dll', '/win32icon:' + str(source / 'icon.ico'),
        '/win32manifest:' + str(source / 'app.manifest'),
        '/resource:' + str(source / 'icon.ico') + ',InstallerIcon',
        '/resource:' + str(source / 'capsule.png') + ',InstallerLogo',
        '/resource:' + str(payload) + ',payload.zip',
        str(source / 'Guestbook_Installer.cs'), str(source / 'BuildManifest.cs'),
    ], check=True)
    for folder, instruction in [(auto, 'automatic.txt'), (manual, 'manual.txt')]:
        shutil.copy2(root / 'distribution' / instruction, folder / 'README.txt')
        shutil.copy2(root / 'THIRD_PARTY_NOTICES.txt', folder / '제3자_라이선스_고지.txt')
    checksums = []
    for folder, name in [(auto, 'Guestbook_Korean_Patch_v1.0.0.zip'),
                         (manual, 'Guestbook_Korean_Patch_Manual_v1.0.0.zip')]:
        archive_path = out / name
        with zipfile.ZipFile(archive_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for file in sorted(folder.rglob('*')):
                if file.is_file():
                    archive.write(file, file.relative_to(folder).as_posix())
        checksums.append(sha(archive_path) + '  ' + name)
    (out / 'SHA256SUMS.txt').write_text('\n'.join(checksums) + '\n', encoding='utf-8')
    print('자동·수동 배포본 및 SHA256SUMS.txt 생성 완료')


if __name__ == '__main__':
    main()
