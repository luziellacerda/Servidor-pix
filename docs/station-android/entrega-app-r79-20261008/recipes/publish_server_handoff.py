"""Publish R79 synopsis documents with a separate index; never modify server checkout."""
from pathlib import Path
import argparse, hashlib, importlib.util, json, re

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT.parents[1]
spec = importlib.util.spec_from_file_location('r77_document_helpers', ROOT.parent / 'station-multiplayer-r77-20261008/recipes/publish_server_handoff.py')
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
git, require, remote_ref, state, SERVER = helper.git, helper.require, helper.remote_ref, helper.state, helper.SERVER
BASE = '525f03735cbec194e942c91417d38a4d799c8910'
BASE_BRANCH = 'docs/station-r78-synopses-20261008'
RETURN = 'af58034595614098ea4df4d959e50947670c080f'
RETURN_BRANCH = 'docs/station-r78-server-review-20261008'
BRANCH = 'docs/station-r79-room-bootstrap-20261008'
APP_BRANCH = 'fix/station-room-bootstrap-r79-20261008'
VERSION = 'versions/station-room-bootstrap-r79-20261008'
PREFIX = 'docs/station-android/entrega-app-r79-20261008/'
DOC = 'docs/station-android/ENTREGA-APP-R79-SALAS-DREAMCAST-20261008.md'
WORK = Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008')


def sha(data): return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app-commit', required=True)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    require(re.fullmatch('[0-9a-f]{40}', args.app_commit), 'Exact public source commit required')
    before = state()
    if args.publish: git(SERVER, 'fetch', 'origin', '+refs/heads/*:refs/remotes/origin/*')
    for branch, commit in ((BASE_BRANCH, BASE), (RETURN_BRANCH, RETURN)):
        require(remote_ref(SERVER, 'refs/heads/' + branch) == commit, 'Known server branch changed; read return first')
    require(remote_ref(APP, 'refs/heads/' + APP_BRANCH) == args.app_commit, 'Exact source must already be pushed')
    require(remote_ref(SERVER, 'refs/heads/' + BRANCH) is None, 'Do not overwrite an existing delivery')
    require(not git(SERVER, 'for-each-ref', '--format=%(refname)', 'refs/heads/' + BRANCH).strip(), 'Local delivery already exists')
    files = {}
    for record in git(APP, 'ls-tree', '-rz', '--full-tree', args.app_commit, '--', VERSION + '/').split(b'\0'):
        if not record: continue
        meta, raw_name = record.split(b'\t', 1)
        mode, kind, object_id = meta.decode().split()
        name = raw_name.decode('utf8')[len(VERSION) + 1:]
        require(mode == '100644' and kind == 'blob' and Path(name).suffix in {'.md', '.json', '.py', '.h', '.cpp', '.java'}, 'Only source/document files allowed')
        content = git(APP, 'cat-file', 'blob', object_id)
        require(b'\0' not in content and len(content) <= 24 * 1024**2, 'Binary or oversized input')
        text = content.decode('utf8')
        require(not re.search(r'-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----|\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b', text), 'Potential credential')
        if name.endswith('.json'): json.loads(text)
        files[name] = content
    require({'README.md', 'STATUS.json', 'HANDOFF-APP-R79-PARA-SERVIDOR-20261008.md', 'data/dreamcast-descriptions.json', 'evidence/package.json'} <= files.keys(), 'Incomplete delivery')
    require(sum(map(len, files.values())) < 64 * 1024**2, 'Unexpected delivery size')
    status = json.loads(files['STATUS.json'])
    require(status['serverDeployed'] is False, 'Do not imply deployment')
    lead = f'''# R79 Correção do app e pendências do servidor

Fonte imutável: `{args.app_commit}`, ramo `{APP_BRANCH}`.
[Fontes, receitas e evidências](https://github.com/luziellacerda/TurboElden/tree/{args.app_commit}/{VERSION}).

Pai `{BASE}` é a entrega anterior do app. Último retorno real: `{RETURN}`.
Esta publicação não implanta Linux nem ativa salas. A falha local de rota foi corrigida; a autorização das novas salas, inclusive duas pessoas, continua exigindo os perfis e o contrato previstos.

'''
    payload = {PREFIX + name: value for name, value in files.items()}
    payload[DOC] = lead.encode('utf8') + files['HANDOFF-APP-R79-PARA-SERVIDOR-20261008.md']
    manifest = dict(appCommit=args.app_commit, serverParent=BASE, latestRealServerReturn=RETURN, docsOnly=True, linuxDeployed=False, apkUploaded=False, files=[dict(path=name, bytes=len(value), sha256=sha(value)) for name, value in sorted(payload.items())])
    payload[PREFIX + 'DELIVERY-FILES.json'] = (json.dumps(manifest, indent=2) + '\n').encode('utf8')
    plan = dict(appCommit=args.app_commit, serverParent=BASE, branch=BRANCH, files=len(payload), bytes=sum(map(len, payload.values())), document=DOC, published=False)
    require(state() == before, 'Server checkout/index changed')
    if not args.publish:
        print(json.dumps(plan, indent=2)); return
    work = WORK / ('publication-' + args.app_commit[:12])
    require(not work.exists(), 'Fresh work directory required'); work.mkdir(parents=True)
    author = git(APP, 'show', '-s', '--format=%an%n%ae', args.app_commit).decode('utf8').splitlines()
    require(len(author) == 2 and all(author), 'Source author identity required')
    env = dict(GIT_INDEX_FILE=str(work / 'server-r79-docs.index'), GIT_AUTHOR_NAME=author[0], GIT_COMMITTER_NAME=author[0], GIT_AUTHOR_EMAIL=author[1], GIT_COMMITTER_EMAIL=author[1])
    git(SERVER, 'read-tree', BASE, env=env)
    for name, content in sorted(payload.items()):
        blob = git(SERVER, 'hash-object', '-w', '--stdin', data=content).decode().strip()
        git(SERVER, 'update-index', '--add', '--cacheinfo', f'100644,{blob},{name}', env=env)
    tree = git(SERVER, 'write-tree', env=env).decode().strip()
    commit = git(SERVER, 'commit-tree', tree, '-p', BASE, data=b'docs(station): deliver R79 room bootstrap fix and Dreamcast catalog request\n', env=env).decode().strip()
    require(set(git(SERVER, 'diff-tree', '--no-commit-id', '--name-only', '-r', commit).decode().splitlines()) == set(payload), 'Unexpected committed path')
    for name, content in payload.items(): require(git(SERVER, 'show', f'{commit}:{name}') == content, 'Committed bytes differ')
    require(state() == before and remote_ref(SERVER, 'refs/heads/' + BRANCH) is None and remote_ref(SERVER, 'refs/heads/' + RETURN_BRANCH) == RETURN, 'Concurrent server change')
    git(SERVER, 'update-ref', 'refs/heads/' + BRANCH, commit, '0' * 40)
    prepared = dict(plan, serverCommit=commit, tree=tree)
    (work / 'prepared.json').write_text(json.dumps(prepared, indent=2) + '\n', 'utf8')
    git(SERVER, 'push', 'origin', f'refs/heads/{BRANCH}:refs/heads/{BRANCH}')
    require(remote_ref(SERVER, 'refs/heads/' + BRANCH) == commit and state() == before, 'Publication or preservation check failed')
    receipt = dict(prepared, published=True, serverCheckoutAndIndexPreserved=True, linuxDeployed=False, apkUploaded=False, url=f'https://github.com/luziellacerda/Servidor-pix/blob/{commit}/{DOC}')
    (work / 'server-publication.json').write_text(json.dumps(receipt, indent=2) + '\n', 'utf8')
    print(json.dumps(receipt, indent=2))


if __name__ == '__main__': main()
