#!/usr/bin/env python3
"""Bounded rollout of fd13c0d and the reconciled Station revision 3.

Targets only turborama-station-api.service. Requires authorized root access.
Existing migrations/keys/configuration are preserved. A failed verification
returns the unit to its original binary and index. Reports contain no secrets.
"""

import argparse
import base64
from collections import Counter
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import socket
import ssl
import subprocess
import sys
import tarfile
import time
import tempfile
from urllib.request import urlopen

from cryptography import x509
from cryptography.hazmat.primitives import serialization


SERVICE = "turborama-station-api.service"
SOURCE_REVISION = "fd13c0d27eaab6a4dcf931a9cd64c3dcd7dd50c4"
DLL_SHA = "f305ae3763cb77a53a27b2c168c8de290191b3a7e88e612850856b6f7be7e639"
OLD_DLL = Path("/opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll")
OLD_DLL_SHA = "75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2"
OLD_INDEX_SHA = "5b3f881479a388694b4b34e33e406770885d0203fa588e5c50ff40d5496fbdf8"
API_SOURCE = Path("/mnt/DADOS/station-api-release-candidate-20261003-fd13c0d")
CONTENT_SOURCE = Path("/mnt/DADOS/station-content-reconciled-20261003-rev3")
CONTENT_INDEX_SHA = "f0661ff46450eed63cb2bd7d8d084c37801116ccfd1e6b687de7a6090b35f6fe"
API_TARGET = Path("/opt/turborama-station-20261003-fd13c0d")
CONTENT_PARENT = Path("/mnt/DADOS/turbostation-releases")
CONTENT_TARGET = CONTENT_PARENT / "station-20261003-fd13c0d/content"
BACKUP = Path("/mnt/DADOS/turbostation-backup-20261003-fd13c0d")
DROPIN = Path("/etc/systemd/system/turborama-station-api.service.d/zz-station-rev3-20261003.conf")
EXTRA_ENV = Path("/etc/turborama-suite/station-rev3-20261003.env")
ORIGINAL_ENV = Path("/etc/turborama-suite/station-5192.env")
UNIT = Path("/etc/systemd/system/turborama-station-api.service")
STATION_NGINX = Path("/etc/nginx/snippets/turborama-station.locations.conf")
STATION_NGINX_SHA = "e19d6cba1407abc64be9dbe8f217d69db2f3bbded177c982d9e97336f6d56a44"
FINAL_INDEX_SHA = "5b460a6f9866e30a5a5b4dad24652c512b1187b3df01244e6af9308ae6b18342"
PIN = "13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7"
KEY_ID = "06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268"
SHARED = ("turborama-pix.service", "turborama-suite-api.service", "turborama-suite-admin.service",
          "turborama-suite-content-gateway.service", "nginx.service", "cloudflared.service",
          "postgresql@16-main.service", "redis-server.service")
ENV_TEXT = "Station__LibraryIndexFile=" + str(CONTENT_TARGET / "index.json") + "\n"
DROPIN_TEXT = ("[Service]\nWorkingDirectory=" + str(API_TARGET) +
    "\nExecStart=\nExecStart=/usr/bin/dotnet " + str(API_TARGET / OLD_DLL.name) +
    "\nEnvironmentFile=" + str(EXTRA_ENV) + "\n")


def run(command, timeout=60, **kwargs):
    result = subprocess.run(command, capture_output=True, text=True, timeout=timeout, **kwargs)
    if result.returncode:
        raise RuntimeError("bounded command failed")
    return result.stdout


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda:source.read(1024*1024), b""): h.update(block)
    return h.hexdigest()


def private_text(path, text):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd,"w") as output:
        output.write(text);output.flush();os.fsync(output.fileno())


def replace_config(path, text):
    if path.is_symlink():raise ValueError("configuration is a link")
    previous = path.stat()
    fd, temporary = tempfile.mkstemp(prefix=".station-release-",dir=path.parent)
    try:
        os.fchmod(fd,previous.st_mode&0o777)
        os.fchown(fd,previous.st_uid,previous.st_gid)
        with os.fdopen(fd,"w") as output:
            output.write(text);output.flush();os.fsync(output.fileno())
        os.replace(temporary,path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def state(unit):
    return run(["systemctl","show",unit,"-p","MainPID","-p","ActiveState"]).strip()


def runtime():
    pid = int(run(["systemctl","show",SERVICE,"-p","MainPID","--value"]).strip())
    if pid <= 0: raise ValueError("Station must be running for preflight")
    values = dict(entry.decode().split("=",1) for entry in
                  Path(f"/proc/{pid}/environ").read_bytes().split(b"\0") if b"=" in entry)
    ids = {}
    for line in Path(f"/proc/{pid}/status").read_text().splitlines():
        key,_,value = line.partition(":")
        if key in {"Uid","Gid","Groups"}: ids[key] = [int(n) for n in value.split()]
    return values, ids


def database_name(values):
    text = values.get("ConnectionStrings__SuiteStore", "")
    def field(name):
        m = re.search(r"(?:^|;)\s*(?:"+name+r")\s*=\s*([^;]+)",text,re.I)
        return m[1].strip().strip('\"\'') if m else ""
    db = field("Database|Initial Catalog")
    if not re.fullmatch(r"[A-Za-z0-9_-]+",db) or field("Host") not in \
            {"127.0.0.1","localhost","/var/run/postgresql"} or field("Port") not in {"","5432"}:
        raise ValueError("database identity differs from the approved local target")
    return db


def sql(db, statement):
    return run(["runuser","-u","postgres","--","psql","-X","-qAt","-v",
                "ON_ERROR_STOP=1","--dbname",db,"-c",statement])


def ready(base, process=None):
    until = time.monotonic()+60
    while time.monotonic() < until:
        if process is not None and process.poll() is not None:
            raise ValueError("candidate stopped during startup")
        try:
            with urlopen(base+"/ready/station",timeout=2) as response:
                if response.status == 200: return
        except Exception: pass
        time.sleep(0.5)
    raise TimeoutError("Station readiness timed out")


def restore_check():
    root = os.environ.get("PG_CLUSTER_CONF_ROOT", "")
    if not root.startswith("/tmp/pg_virtualenv.") or not Path(root).is_dir():
        raise ValueError("restore requires an isolated pg_virtualenv")
    directory = run(["psql","-X","-qAt","-c","SHOW data_directory"]).strip()
    if not directory.startswith("/tmp/pg_virtualenv."):
        raise ValueError("restore refused a production cluster")
    run(["createdb","station_restore_check"])
    run(["pg_restore","--exit-on-error","--no-owner","--no-acl","--dbname",
         "station_restore_check",str(BACKUP / "database.dump")],timeout=300)
    verified = run(["psql","-X","-qAt","--dbname","station_restore_check","-c",
        "SELECT count(*) FROM suite.schema_migrations WHERE version IN "
        "('028_station_android','029_station_download_grants')"]).strip()
    if verified != "2": raise ValueError("restored migration ledger differs")
    print(json.dumps({"databaseRestoreVerified":True,"temporaryCluster":True}),flush=True)


def rollback():
    metadata = json.loads((BACKUP / "state.json").read_text())
    if digest(OLD_DLL) != OLD_DLL_SHA or digest(Path(metadata["originalIndex"])) != OLD_INDEX_SHA:
        raise ValueError("original rollback artifacts changed")
    for path, expected in ((DROPIN,DROPIN_TEXT),(EXTRA_ENV,ENV_TEXT)):
        if path.exists():
            if path.is_symlink() or path.read_text() != expected:
                raise ValueError("a rollback configuration changed")
            path.unlink()
    nginx_backup = BACKUP/"station-nginx.conf"
    if nginx_backup.exists():
        previous = nginx_backup.read_text()
        expected = previous.replace("proxy_set_header X-Correlation-ID $request_id;",
                                    "proxy_set_header X-Correlation-ID $http_x_correlation_id;")
        if digest(nginx_backup) != STATION_NGINX_SHA or STATION_NGINX.is_symlink() or \
                STATION_NGINX.read_text() not in {previous,expected}:
            raise ValueError("Station Nginx rollback configuration changed")
        replace_config(STATION_NGINX,previous)
        run(["nginx","-t"])
        run(["systemctl","reload","nginx.service"])
    run(["systemctl","daemon-reload"])
    run(["systemctl","restart",SERVICE])
    ready("http://127.0.0.1:5192")


def activate_prepared():
    report = {"service":SERVICE,"sourceRevision":SOURCE_REVISION,"dllSha256":DLL_SHA,
              "applied":False,"migrationApplied":False}
    configured = False
    stage = "prepared_preflight"
    try:
        previous = json.loads((BACKUP/"result.json").read_text())
        if previous.get("rolledBack") is not True or previous.get("backupRestoreVerified") is not True:
            raise ValueError("a verified, rolled-back preparation is required")
        values,ids = runtime()
        original_index = Path(values["Station__LibraryIndexFile"])
        if digest(OLD_DLL)!=OLD_DLL_SHA or digest(original_index)!=OLD_INDEX_SHA or \
                digest(API_TARGET/OLD_DLL.name)!=DLL_SHA or \
                digest(CONTENT_TARGET/"index.json")!=FINAL_INDEX_SHA or \
                digest(STATION_NGINX)!=STATION_NGINX_SHA:
            raise ValueError("prepared artifacts or effective production changed")
        if DROPIN.exists() or EXTRA_ENV.exists() or \
                run(["systemctl","show",SERVICE,"-p","DropInPaths","--value"]).strip():
            raise ValueError("Station configuration changed")
        baseline = {unit:state(unit) for unit in SHARED}
        if any("ActiveState=active" not in value for value in baseline.values()):
            raise ValueError("a shared service is not healthy")
        db = database_name(values)
        public = serialization.load_pem_private_key(
            Path(values["Station__AssertionPrivateKeyPemFile"]).read_bytes(),password=None).public_key()
        spki = public.public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
        if hashlib.sha256(spki).hexdigest()!=KEY_ID:raise ValueError("assertion key changed")
        pepper = base64.b64decode(Path(values["Station__ActivationPepperFile"]).read_bytes())
        index = json.loads((CONTENT_TARGET/"index.json").read_text())
        spec = importlib.util.spec_from_file_location("station_release_checks",
                    Path(__file__).with_name("verificar-http-release-station.py"))
        helper = importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
        stage = "station_correlation_proxy"
        original_nginx = STATION_NGINX.read_text()
        if original_nginx.count("proxy_set_header X-Correlation-ID $request_id;")!=2:
            raise ValueError("unexpected Station proxy layout")
        private_text(BACKUP/"station-nginx.conf",original_nginx)
        configured = True
        replace_config(STATION_NGINX,original_nginx.replace("proxy_set_header X-Correlation-ID $request_id;",
            "proxy_set_header X-Correlation-ID $http_x_correlation_id;"))
        run(["nginx","-t"])
        run(["systemctl","reload","nginx.service"])
        report["nginxStationCorrelationUpdated"] = True
        stage = "activate_prepared_station"
        private_text(EXTRA_ENV,ENV_TEXT)
        private_text(DROPIN,DROPIN_TEXT)
        run(["systemctl","daemon-reload"])
        run(["systemctl","restart",SERVICE])
        ready("http://127.0.0.1:5192")
        running,_ = runtime()
        if running["Station__LibraryIndexFile"] != str(CONTENT_TARGET/"index.json"):
            raise ValueError("effective index differs")
        stage = "public_https_verification"
        check = helper.StationReleaseVerification(lambda statement:sql(db,statement),pepper,public,index)
        try:
            check.create()
            report["publicHttpsVerification"] = check.check("https://app.lzgames.com.br",
                catalog_output=BACKUP/"public-catalog.tsv")
        finally:
            if not check.cleanup():raise ValueError("synthetic verification license remains")
        if any(state(u)!=s for u,s in baseline.items()):raise ValueError("a shared service changed")
        export = Path("/home/lz-servidor/catalogo-station-5192-verificado-20261003.tsv")
        private_text(export,(BACKUP/"public-catalog.tsv").read_text())
        import pwd
        owner = pwd.getpwnam("lz-servidor")
        os.chown(export,owner.pw_uid,owner.pw_gid)
        report.update({"applied":True,"catalogItems":1816,"compatibilityItems":255,
            "indexSha256":FINAL_INDEX_SHA,"backupRestoreVerified":True,
            "serviceReadableVerifiedFiles":previous["serviceReadableVerifiedFiles"],
            "platformCounts":report["publicHttpsVerification"]["platformCounts"],
            "sharedServicesPreserved":True,"syntheticLicensesRemoved":True,
            "publicCatalogTsvSha256":digest(export),"pid":int(run(["systemctl","show",SERVICE,
                "-p","MainPID","--value"]).strip())})
    except Exception as error:
        report["failedStage"] = stage;report["errorType"] = type(error).__name__
        if type(error) is ValueError and str(error) in {
                "HTTP correlation or redirect policy failed","signed identity differs",
                "signed catalog differs from the reviewed index","cover status, MIME or bytes differ",
                "signed grant differs","artifact status, length or SHA256 differs",
                "profile projection differs","grant reuse was accepted"}:
            report["verificationFailure"] = str(error)
        if configured:
            try:rollback();report["rolledBack"] = True
            except Exception as rollback_error:report["rollbackErrorType"] = type(rollback_error).__name__
    finally:
        if BACKUP.is_dir() and not (BACKUP/"activation-result.json").exists():
            private_text(BACKUP/"activation-result.json",json.dumps(report,sort_keys=True))
    print(json.dumps(report,sort_keys=True),flush=True)
    return 0 if report["applied"] else 2


def apply():
    report = {"service":SERVICE,"sourceRevision":SOURCE_REVISION,"dllSha256":DLL_SHA,
              "applied":False,"migrationApplied":False}
    configured = False
    stage = "preflight"
    try:
        values, ids = runtime()
        original_index = Path(values["Station__LibraryIndexFile"])
        if ids["Uid"][1] != 995 or digest(OLD_DLL) != OLD_DLL_SHA or \
                digest(original_index) != OLD_INDEX_SHA or digest(CONTENT_SOURCE/"index.json") != CONTENT_INDEX_SHA:
            raise ValueError("effective Station state differs from the reviewed target")
        if run(["systemctl","show",SERVICE,"-p","DropInPaths","--value"]).strip():
            raise ValueError("unexpected Station drop-in")
        actual_start = run(["systemctl","show",SERVICE,"-p","ExecStart","--value"])
        if str(OLD_DLL) not in actual_start: raise ValueError("effective binary differs")
        if digest(API_SOURCE/OLD_DLL.name) != DLL_SHA or \
                SOURCE_REVISION.encode() not in (API_SOURCE/OLD_DLL.name).read_bytes():
            raise ValueError("release hash or source revision differs")
        for path in (BACKUP, API_TARGET, CONTENT_TARGET.parent, DROPIN, EXTRA_ENV):
            if path.exists() or path.is_symlink(): raise ValueError("rollout target already exists")
        if values.get("Station__Enabled","").lower() != "true": raise ValueError("Station is disabled")
        baseline = {unit:state(unit) for unit in SHARED}
        if any("ActiveState=active" not in value for value in baseline.values()):
            raise ValueError("a shared service is not healthy")
        db = database_name(values)
        ledger = sql(db,"BEGIN READ ONLY; SELECT count(*) FROM suite.schema_migrations WHERE version IN "
            "('028_station_android','029_station_download_grants'); ROLLBACK;").strip()
        if ledger != "2": raise ValueError("Station migrations are missing")
        public_pem = Path(values["Station__AssertionPrivateKeyPemFile"]).read_bytes()
        public = serialization.load_pem_private_key(public_pem,password=None).public_key()
        spki = public.public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
        if hashlib.sha256(spki).hexdigest() != KEY_ID: raise ValueError("assertion key differs")
        pepper = base64.b64decode(Path(values["Station__ActivationPepperFile"]).read_bytes())
        with socket.create_connection(("app.lzgames.com.br",443),timeout=15) as sock:
            with ssl.create_default_context().wrap_socket(sock,server_hostname="app.lzgames.com.br") as tls:
                certificate = x509.load_der_x509_certificate(tls.getpeercert(binary_form=True))
                edge_spki = certificate.public_key().public_bytes(serialization.Encoding.DER,
                    serialization.PublicFormat.SubjectPublicKeyInfo)
                if hashlib.sha256(edge_spki).hexdigest() != PIN: raise ValueError("TLS pin differs")
        report["tlsPinVerified"] = True
        stage = "backup"
        BACKUP.mkdir(mode=0o700)
        private_text(BACKUP/"state.json",json.dumps({"originalIndex":str(original_index),
            "originalIndexSha256":OLD_INDEX_SHA,"originalDllSha256":OLD_DLL_SHA,
            "sharedBaseline":baseline},sort_keys=True))
        archive_path = BACKUP/"files.tar"
        with tarfile.open(archive_path,"x") as archive:
            for source,name in ((original_index,"index.json"),(ORIGINAL_ENV,"station.env"),
                                (UNIT,"station.service"),(OLD_DLL.parent,"api")):
                archive.add(source,arcname=name)
        archive_path.chmod(0o600)
        with tarfile.open(archive_path,"r") as archive:
            for name, source in (("index.json",original_index),("station.env",ORIGINAL_ENV),
                                 ("station.service",UNIT),("api/"+OLD_DLL.name,OLD_DLL)):
                if hashlib.sha256(archive.extractfile(name).read()).hexdigest() != digest(source):
                    raise ValueError("filesystem backup verification failed")
        with (BACKUP/"database.dump").open("xb") as dump:
            os.fchmod(dump.fileno(),0o600)
            result = subprocess.run(["runuser","-u","postgres","--","pg_dump","--format=custom",
                "--dbname",db],stdout=dump,stderr=subprocess.PIPE,timeout=300)
            if result.returncode: raise ValueError("database backup failed")
            dump.flush();os.fsync(dump.fileno())
        restore_output = run(["pg_virtualenv","-t","/usr/bin/python3",str(Path(__file__).resolve()),
                              "--restore-backup"],timeout=360)
        if '"databaseRestoreVerified": true' not in restore_output:
            raise ValueError("database backup was not restored")
        report["backupRestoreVerified"] = True
        print(json.dumps({"stage":"backup_verified","databaseRestoreVerified":True}),flush=True)
        stage = "content_copy"
        for source in (API_SOURCE,CONTENT_SOURCE):
            if source.is_symlink() or any(path.is_symlink() for path in source.rglob("*")):
                raise ValueError("release tree contains a link")
        CONTENT_PARENT.mkdir(mode=0o750,exist_ok=True)
        if CONTENT_PARENT.is_symlink(): raise ValueError("content parent is a link")
        os.chown(CONTENT_PARENT,0,ids["Gid"][1])
        CONTENT_PARENT.chmod(0o750)
        CONTENT_TARGET.parent.mkdir(mode=0o750)
        os.chown(CONTENT_TARGET.parent,0,ids["Gid"][1])
        CONTENT_TARGET.parent.chmod(0o750)
        shutil.copytree(API_SOURCE,API_TARGET)
        shutil.copytree(CONTENT_SOURCE,CONTENT_TARGET)
        index = json.loads((CONTENT_TARGET/"index.json").read_text())
        for row in index["items"]:
            for field in ("filePath","coverPath"):
                relative = Path(row[field]).relative_to(CONTENT_SOURCE)
                row[field] = str(CONTENT_TARGET/relative)
        visible = [row for row in index["items"] if row.get("catalogVisible") is not False]
        if len(visible) != 1816 or len(index["items"]) != 2071 or index["revision"] != 3:
            raise ValueError("reconciled counts differ")
        if not {r["itemId"] for r in json.loads(original_index.read_text())["items"]}.issubset(
                {r["itemId"] for r in index["items"]}):
            raise ValueError("a published item ID was lost")
        (CONTENT_TARGET/"index.json").write_text(json.dumps(index,ensure_ascii=False,separators=(",",":"))+"\n")
        for root, directory_mode, file_mode, group in ((API_TARGET,0o755,0o644,0),
                (CONTENT_TARGET,0o750,0o640,ids["Gid"][1])):
            for path in [root,*root.rglob("*")]:
                os.chown(path,0,group);path.chmod(directory_mode if path.is_dir() else file_mode)
        expected_files = []
        for line in (CONTENT_TARGET/"files.sha256").read_text().splitlines():
            sha, relative = line.split("  ",1)
            path = CONTENT_TARGET/relative
            if not path.resolve().is_relative_to(CONTENT_TARGET): raise ValueError("manifest path escaped")
            expected_files.append({"path":str(path),"sha256":sha})
        worker = '''import hashlib,json,sys
files=json.load(sys.stdin)
for item in files:
 h=hashlib.sha256()
 with open(item['path'],'rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 if h.hexdigest()!=item['sha256']:raise ValueError('release hash mismatch')
print(json.dumps({'serviceReadableVerifiedFiles':len(files)}))
'''
        report["serviceReadableVerifiedFiles"] = json.loads(run(["/usr/bin/python3","-c",worker],
            input=json.dumps(expected_files),user=ids["Uid"][1],group=ids["Gid"][1],
            extra_groups=ids["Groups"],cwd="/",timeout=120))["serviceReadableVerifiedFiles"]
        report["indexSha256"] = digest(CONTENT_TARGET/"index.json")
        report["catalogItems"] = len(visible)
        report["compatibilityItems"] = len(index["items"])-len(visible)
        report["platformCounts"] = dict(sorted(Counter(row["platform"] for row in visible).items()))
        spec = importlib.util.spec_from_file_location("station_release_checks",
                    Path(__file__).with_name("verificar-http-release-station.py"))
        helper = importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
        def verify(base):
            check = helper.StationReleaseVerification(lambda statement:sql(db,statement),pepper,public,index)
            try:
                check.create()
                return check.check(base)
            finally:
                if not check.cleanup(): raise ValueError("synthetic verification license remains")
        stage = "shadow_verification"
        with socket.socket() as probe:
            probe.bind(("127.0.0.1",0));port = probe.getsockname()[1]
        shadow_base = "http://127.0.0.1:"+str(port)
        shadow_env = dict(values,Station__LibraryIndexFile=str(CONTENT_TARGET/"index.json"))
        for name in ("INVOCATION_ID","NOTIFY_SOCKET","LISTEN_FDS","LISTEN_PID",
                     "LISTEN_FDNAMES","JOURNAL_STREAM"):
            shadow_env.pop(name,None)
        with (BACKUP/"shadow.log").open("xb") as log:
            os.fchmod(log.fileno(),0o600)
            process = subprocess.Popen(["/usr/bin/dotnet",str(API_TARGET/OLD_DLL.name),"--urls",shadow_base],
                env=shadow_env,cwd=API_TARGET,stdout=log,stderr=log,user=ids["Uid"][1],
                group=ids["Gid"][1],extra_groups=ids["Groups"])
            try:
                ready(shadow_base,process)
                report["shadowVerification"] = verify(shadow_base)
            finally:
                process.terminate()
                try:process.wait(timeout=15)
                except subprocess.TimeoutExpired:process.kill();process.wait()
        print(json.dumps({"stage":"candidate_verified","catalogItems":1816,"pairs":5}),flush=True)
        stage = "activate_station"
        if digest(original_index) != OLD_INDEX_SHA or any(state(u)!=s for u,s in baseline.items()):
            raise ValueError("production changed before activation")
        DROPIN.parent.mkdir(mode=0o755,exist_ok=True)
        private_text(EXTRA_ENV,ENV_TEXT)
        private_text(DROPIN,DROPIN_TEXT)
        configured = True
        run(["systemctl","daemon-reload"])
        run(["systemctl","restart",SERVICE])
        ready("http://127.0.0.1:5192")
        running,_ = runtime()
        if running["Station__LibraryIndexFile"] != str(CONTENT_TARGET/"index.json"):
            raise ValueError("effective index override did not apply")
        stage = "public_https_verification"
        report["publicHttpsVerification"] = verify("https://app.lzgames.com.br")
        if any(state(u)!=s for u,s in baseline.items()): raise ValueError("a shared service changed")
        report["sharedServicesPreserved"] = True
        report["syntheticLicensesRemoved"] = True
        report["pid"] = int(run(["systemctl","show",SERVICE,"-p","MainPID","--value"]).strip())
        report["applied"] = True
        stage = "complete"
    except Exception as error:
        report["failedStage"] = stage
        report["errorType"] = type(error).__name__
        if configured:
            try:rollback();report["rolledBack"] = True
            except Exception as rollback_error:report["rollbackErrorType"] = type(rollback_error).__name__
    finally:
        if BACKUP.is_dir():
            destination = BACKUP/"result.json"
            if not destination.exists():private_text(destination,json.dumps(report,sort_keys=True))
    print(json.dumps(report,sort_keys=True),flush=True)
    return 0 if report["applied"] else 2


if __name__ == "__main__":
    os.umask(0o077)
    sys.dont_write_bytecode = True
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group(required=True)
    actions.add_argument("--apply",action="store_true")
    actions.add_argument("--rollback",action="store_true")
    actions.add_argument("--activate-prepared",action="store_true")
    actions.add_argument("--restore-backup",action="store_true")
    args = parser.parse_args()
    if os.geteuid()!=0:parser.error("use authorized root access")
    if args.restore_backup:restore_check()
    elif args.rollback:rollback();print('{"rolledBack":true}')
    elif args.activate_prepared:raise SystemExit(activate_prepared())
    else:raise SystemExit(apply())
