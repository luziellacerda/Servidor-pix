"""Record actual R78 local build/package receipts; no publication or deployment."""
from pathlib import Path
import datetime, hashlib, json, subprocess

ROOT = Path(__file__).resolve().parent.parent
REPO = ROOT.parents[1]


def load(name): return json.loads((ROOT / name).read_text('utf8'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    generation = load('evidence/synopses-generation.json')
    build = load('evidence/carousel-build.json')
    package = load('evidence/package.json')
    storage = load('evidence/storage-decision.json') if (ROOT / 'evidence/storage-decision.json').exists() else None
    retained = bool(storage and storage.get('approvedRetentionInBuildWorkspace'))
    if retained:
        assert storage['artifactPath'] == package['temporaryApk'] and storage['artifactSHA256'] == package['sha256']
    assert build['compiled'] and package['allPackageEntriesVerified']
    assert generation['ids'] == 2467 and generation['missing'] == 0
    assert sha(ROOT / 'data/synopses-complete.json') == generation['dataSHA256']
    assert sha(ROOT / 'native/station_game_infos.h') == generation['headerSHA256']
    assert package['carouselSHA256'] == build['nativeSHA256']
    parent = subprocess.check_output(['git', '-c', 'safe.directory=' + REPO.as_posix(), '-C', str(REPO), 'rev-parse', '04a58b7^{commit}'], text=True).strip()
    state = dict(version='R78', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), state='compiled-signed-candidate-not-installed',
        baseAppCommit=parent, baseApkSHA256=package['baseSHA256'], apkSHA256=package['sha256'], apkBytes=package['bytes'],
        temporaryApk=package['temporaryApk'], finalApk=package['finalApk'], certificateSHA256=package['certificateSHA256'],
        userApprovedApkRetentionInE=retained,
        carouselSHA256=build['nativeSHA256'], changedNativeSources=build['changedNativeSources'], addedNativeSources=build['addedNativeSources'],
        packageEntriesPreserved=package['preservedEntries'], videosPreserved=package['totalVideos'], changes=package['changes'],
        catalogIds=2467, visibleIds=2212, compatibilityIds=255, missingSynopses=0, recoveredAliases=6,
        restoredLongSynopses=17, originalEditorialUpdates=14, editorialPendingPreserved=5,
        serverSynopsisCandidates=64, serverNativeOnlyLongTexts=17, descriptionLimitUtf16=2000,
        synopsisDataSHA256=generation['dataSHA256'], headerSHA256=generation['headerSHA256'], localTests=build['results'],
        installed=False, physicalDisplayVerified=False, serverDeployed=False, existingInstalledVersion='R76',
        allHistoricalFactsVerified=False, liveCatalogRead=False, runtimeDexEnginesChangedFromR77=False,
        newEngineRegistrationForSynopsisChangeRequired=False, inheritedR77ProductionQualificationStillRequired=True,
        latestRealServerReturn='ae77b9cca7fc881771bdb809a308e40bafaad1c5',
        pending=['Physical display verification', 'Operator comparison against the current live catalogue',
                 'Five short synopses retain prior text pending stronger documentation',
                 'R77 protocol/profile and multiplayer qualification remains separate'])
    if not package['finalApk'] and not retained:
        state['pending'].append('G: lacks room for another APK; signed candidate retained in the authorized E: build workspace')
    (ROOT / 'STATUS.json').write_text(json.dumps(state, ensure_ascii=False, indent=2) + '\n', 'utf8')
    report = f'''# Recibo local R78

- APK: `{package['sha256']}`; {package['bytes']:,} bytes.
- Biblioteca: `{build['nativeSHA256']}`; baseline R77 reproduzida byte a byte.
- Entrada alterada: `{package['changes'][0]}`. Outras {package['preservedEntries']:,} entradas e os 59 vídeos conferidos por hash.
- Catálogo: 2.467 IDs com prosa, zero vazio; seis aliases, 17 continuações e 14 revisões editoriais.
- Certificado preservado. Não instalado. Dados pessoais e emuladores não foram operados.
- APK temporário assinado: `{package['temporaryApk']}`.
- Armazenamento: `{package['finalApk'] or ('mantido pronto em E: por escolha expressa do usuário; nenhum APK anterior apagado' if retained else 'G: pendente de espaço; nenhum APK anterior apagado')}`.

## Testes locais

'''
    report += '\n'.join('- ' + name + ': `' + result + '`.' for name, result in build['results'].items()) + '\n'
    report += '\nEsses resultados não são conferência visual no Android nem revisão factual integral de todos os textos. A base R77 conserva suas pendências de qualificação de salas; aparelhos continuam na R76.\n'
    (ROOT / 'BUILD-RESULT.md').write_text(report, 'utf8')
    records = [{'path': path.relative_to(ROOT).as_posix(), 'bytes': path.stat().st_size, 'sha256': sha(path)}
               for path in sorted(ROOT.rglob('*')) if path.is_file() and path.name != 'SOURCE-FILES.json']
    (ROOT / 'SOURCE-FILES.json').write_text(json.dumps(dict(version='R78', files=records), indent=2) + '\n', 'utf8')
    print(json.dumps({key:state[key] for key in ('version','state','apkSHA256','catalogIds','missingSynopses','installed','finalApk')}, indent=2))


if __name__ == '__main__': main()
