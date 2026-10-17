#!/usr/bin/env python3
"""Capture one N64 attempt through ADB. Standard library; Windows/Linux/macOS.
Raw output is private. This tool does not validate gameplay or install an APK.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import time

PACKAGE = 'org.turboramastation.frontend'
DEBUG_TAGS = ('StationRooms', 'StationRecovery', 'StationN64', 'CoreService', 'CoreInterface')
FILTERS = [x + ':V' for x in DEBUG_TAGS + ('Core', 'Video', 'Audio', 'Input', 'RSP', 'AEVidExt', 'mupen64plus-video-glide64mk2')]
FILTERS += ['AndroidRuntime:E', 'libc:E', 'DEBUG:E', 'libEGL:W', 'OpenGLRenderer:W', 'SurfaceView:W', '*:S']

def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()

def find_adb(explicit):
    candidates = [explicit, shutil.which('adb'),
        str(Path.home() / 'AppData/Local/Android/Sdk/platform-tools/adb.exe'),
        str(Path.home() / 'android-sdk/platform-tools/adb')]
    for value in candidates:
        if value and Path(value).is_file():
            return str(Path(value).resolve())
    raise RuntimeError('ADB não encontrado. Informe --adb com o caminho do adb/adb.exe.')

class Capture:
    def __init__(self, args):
        self.args = args
        self.adb = find_adb(args.adb)
        r = subprocess.run([self.adb, 'devices'], capture_output=True, text=True, timeout=10)
        devices = [l.split()[0] for l in r.stdout.splitlines()[1:] if len(l.split()) >= 2 and l.split()[1] == 'device']
        if args.serial:
            if args.serial not in devices:
                raise RuntimeError('O telefone selecionado não está autorizado no ADB.')
            self.serial = args.serial
        elif len(devices) == 1:
            self.serial = devices[0]
        else:
            raise RuntimeError('Conecte e autorize um telefone; com vários, selecione --serial.')
        stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
        base = args.output or Path.home() / 'StationDiagnostics'
        self.folder = Path(base) / ('n64-' + args.role + '-' + stamp)
        self.folder.mkdir(parents=True, mode=0o700, exist_ok=False)
        self.original = {}
        self.deadline = None
        (self.folder / ".gitignore").write_text("*\n", encoding="utf-8")
        self.manifest = dict(schema='station.n64.private-capture.v1', startedUtc=utc(), role=args.role,
            deviceReference=hashlib.sha256(self.serial.encode()).hexdigest()[:12],
            durationSeconds=args.duration, package=PACKAGE, snapshots=[],
            apkOrRomHashesNotComputed=True, apkIdentityNotProvenByVersionName=True,
            gfxinfoIsUiNotNativeFps=True, surfacePresentationIsNotNativeViCount=True,
            historicalExitInfoRequiresTimestampCorrelation=True, physicalGameplayValidated=False,
            captureCompleted=False)

    def adb_call(self, *args, timeout=10):
        if self.deadline is not None:
            remaining = self.deadline - time.monotonic()
            if remaining <= 0:
                return dict(exitCode=None, output='', error='CAPTURE_DEADLINE')
            timeout = min(timeout, max(.1, remaining))
        try:
            r = subprocess.run([self.adb, '-s', self.serial, *args], capture_output=True,
                text=True, encoding='utf-8', errors='replace', timeout=timeout)
            return dict(exitCode=r.returncode, output=r.stdout, error=r.stderr)
        except subprocess.TimeoutExpired:
            return dict(exitCode=None, output='', error='TIMEOUT')

    def shell(self, *args):
        return self.adb_call('shell', shlex.join(args))

    def save(self, name, value):
        p = self.folder / name
        p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        p.chmod(0o600)

    def snapshot(self, number):
        data = dict(utc=utc(), phoneEpoch=self.shell('date', '+%s'))
        for label, args in [('mainPid', ('pidof', PACKAGE)), ('corePid', ('pidof', PACKAGE + ':n64core')),
            ('services', ('dumpsys', 'activity', 'services', PACKAGE)),
            ('uiFrameStats', ('dumpsys', 'gfxinfo', PACKAGE, 'framestats')),
            ('windowState', ('dumpsys', 'window', 'windows'))]:
            data[label] = self.shell(*args)
        pid_text = data['corePid']['output'].split()
        if pid_text and all(x.isdigit() for x in pid_text):
            data['coreCpu'] = self.shell('top', '-b', '-n', '1', '-p', ','.join(pid_text))
        a = self.shell('dumpsys', 'activity', 'activities')
        data['foreground'] = dict(exitCode=a['exitCode'], error=a['error'], output='\n'.join(
            l for l in a['output'].splitlines() if any(k in l for k in ['mResumedActivity', 'topResumedActivity', 'mFocusedApp'])))
        surfaces = self.shell('dumpsys', 'SurfaceFlinger', '--list')
        layers = [l for l in surfaces['output'].splitlines() if PACKAGE in l or 'paulscode.android.mupen64plusae' in l][:6]
        data['surfaceListStatus'] = dict(exitCode=surfaces['exitCode'], error=surfaces['error'])
        data['surfaces'] = [dict(layer=l, latency=self.shell('dumpsys', 'SurfaceFlinger', '--latency', l)) for l in layers]
        self.save('snapshot-%02d.json' % number, data)
        self.manifest['snapshots'].append(dict(number=number, utc=data['utc'], corePidPresent=bool(pid_text) if data['corePid']['exitCode'] == 0 else None, surfaceLayers=len(layers)))

    def run(self):
        process = None
        interrupted = False
        try:
            self.save('package.json', self.shell('dumpsys', 'package', PACKAGE))
            self.save('prior-exit-history.json', self.shell('dumpsys', 'activity', 'exit-info', PACKAGE))
            self.save('device.json', {p:self.shell('getprop', p) for p in [
                'ro.product.model', 'ro.build.version.release', 'ro.build.version.sdk', 'ro.hardware.egl', 'ro.opengles.version']})
            # Explicit optional debug properties are nonpersistent and restored in finally.
            for tag in DEBUG_TAGS:
                key = 'log.tag.' + tag
                r = self.shell('getprop', key)
                if r['exitCode'] != 0 and self.args.debug_tags:
                    raise RuntimeError('Não foi possível ler o estado original das tags.')
                self.original[key] = r['output'].strip()
            self.save('original-log-properties.json', self.original)
            if self.args.debug_tags:
                for key in self.original:
                    r = self.shell('setprop', key, 'V')
                    if r['exitCode'] != 0:
                        raise RuntimeError('Não foi possível habilitar as tags temporárias de diagnóstico.')
            with (self.folder / 'logcat-private.txt').open('wb') as log:
                process = subprocess.Popen([self.adb, '-s', self.serial, 'logcat', '-b', 'main', '-b', 'system',
                    '-b', 'crash', '-v', 'epoch', '-T', '1', *FILTERS], stdout=log, stderr=subprocess.STDOUT)
                print('Captura iniciada. Reproduza uma tentativa nova e mantenha o cabo conectado.', flush=True)
                start = time.monotonic()
                self.deadline = start + self.args.duration
                number = 0
                while time.monotonic() < self.deadline:
                    self.snapshot(number)
                    number += 1
                    remaining = self.args.duration - (time.monotonic() - start)
                    if remaining <= 0 or process.poll() is not None:
                        break
                    time.sleep(min(20, remaining))
                self.manifest['logcatExitBeforeCleanup'] = process.poll()
                if process.poll() is not None:
                    raise RuntimeError('O leitor ADB encerrou antes da conclusão; a captura ficou incompleta.')
                self.manifest['captureCompleted'] = True
        except KeyboardInterrupt:
            interrupted = True
        finally:
            self.deadline = None
            if process is not None and process.poll() is None:
                process.terminate()  # Our PC-side log reader only; the app keeps running.
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
            restored = True
            if self.args.debug_tags:
                for key, value in self.original.items():
                    r = self.shell('setprop', key, value)
                    verify = self.shell('getprop', key)
                    restored = restored and r['exitCode'] == 0 and verify['exitCode'] == 0 and verify['output'].strip() == value
            self.manifest.update(endedUtc=utc(), interrupted=interrupted, debugTagsRequested=self.args.debug_tags,
                originalDebugPropertiesRestored=restored, captureDirectoryIsPrivateEvidence=True)
            self.save('capture-manifest.json', self.manifest)
        print('Pasta privada: ' + str(self.folder), flush=True)
        print('Não adicionar os arquivos capturados ao Git. Enviar por entrega privada.', flush=True)
        if not self.manifest['originalDebugPropertiesRestored']:
            raise RuntimeError('Reconecte o mesmo telefone para restaurar os valores de original-log-properties.json.')
        if interrupted:
            raise RuntimeError('Captura interrompida; os dados parciais foram preservados.')

def main():
    os.umask(0o077)
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--adb', help='Caminho do adb ou adb.exe')
    p.add_argument('--serial', help='Selecionar somente quando houver vários telefones autorizados')
    p.add_argument('--role', choices=['host', 'guest'], required=True)
    p.add_argument('--duration', type=int, default=120)
    p.add_argument('--output', type=Path, help='Pasta privada; padrão: StationDiagnostics no diretório do usuário')
    p.add_argument('--debug-tags', action='store_true', help='Habilitar temporariamente cinco tags e restaurar seus valores ao terminar')
    args = p.parse_args()
    if not 10 <= args.duration <= 180:
        p.error('--duration deve ficar entre 10 e 180 segundos')
    Capture(args).run()

if __name__ == '__main__':
    main()
