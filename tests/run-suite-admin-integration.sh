#!/bin/bash
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d /tmp/suite-admin-integration.XXXXXX)
pg="suite-admin-integration-$$";admin_pid=;pix_pid=
cleanup(){ set +e; [ -n "$pix_pid" ] && kill "$pix_pid" 2>/dev/null; [ -n "$admin_pid" ] && kill "$admin_pid" 2>/dev/null; docker rm -f "$pg" >/dev/null 2>&1; rm -rf "$work"; }
trap cleanup EXIT
db_password=$(openssl rand -base64 24 | tr -d '\n'); admin_password=$(openssl rand -base64 24 | tr -d '\n')
openssl rand -base64 48 > "$work/token";openssl rand -base64 32 > "$work/pepper";chmod 640 "$work/token" "$work/pepper"
openssl rand -base64 32 | tr -d '\n' > "$work/integrity";openssl rand -base64 32 | tr -d '\n' > "$work/encryption"
printf '%s\n%s\n' "$admin_password" "$admin_password" | dotnet "$root/src/TurboRamaPixOnlineServer/bin/Release/net8.0/TurboRamaPixOnlineServer.dll" --hash-admin-password | tail -1 > "$work/hash"
docker run -d --name "$pg" -e "POSTGRES_PASSWORD=$db_password" -p 127.0.0.1:55449:5432 postgres:16 >/dev/null
sleep 3
for migration in 001_suite_foundation.up.sql 002_lifetime_single_active_device.up.sql 003_admin_otp_audit.up.sql;do docker exec -i "$pg" psql -U postgres -v ON_ERROR_STOP=1 < "$root/migrations/suite/$migration" >/dev/null;done
docker exec "$pg" psql -U postgres -v ON_ERROR_STOP=1 -c "INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices) VALUES('TS-ADMIN-INTEGRATION','TURBORAMA_SUITE','ACTIVE',NULL,NULL,false,'LIFETIME',NULL,'SOFTWARE_ONLY',1);INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint) VALUES('TS-ADMIN-INTEGRATION',repeat('a',64),'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',repeat('A',344),repeat('b',64));" >/dev/null
mkdir "$work/socket" "$work/keys";chmod 750 "$work/socket"
env SUITE_ADMIN_SOCKET="$work/socket/admin.sock" SUITE_ADMIN_TOKEN_FILE="$work/token" SUITE_ADMIN_PEPPER_FILE="$work/pepper" SUITE_ADMIN_CONNECTION="Host=127.0.0.1;Port=55449;Database=postgres;Username=postgres;Password=$db_password" dotnet "$root/src/TurboRamaSuiteAdminServer/bin/Release/net8.0/TurboRamaSuiteAdminServer.dll" >"$work/admin.log" 2>&1 & admin_pid=$!
env ASPNETCORE_URLS=http://127.0.0.1:55188 TURBORAMA_SERVER_STATE_FILE="$work/state.json" TURBORAMA_SERVER_STATE_KEY="$(cat "$work/integrity")" TURBORAMA_SERVER_SECRET_KEY="$(cat "$work/encryption")" TURBORAMA_ALLOW_HTTP_LOOPBACK=true TURBORAMA_ADMIN_USERNAME=turborama-admin TURBORAMA_ADMIN_PASSWORD_HASH="$(cat "$work/hash")" TURBORAMA_ADMIN_PUBLIC_HOST=admin.test.local TURBORAMA_ADMIN_KEY_DIRECTORY="$work/keys" TURBORAMA_SUITE_ADMIN_SOCKET="$work/socket/admin.sock" TURBORAMA_SUITE_ADMIN_TOKEN_FILE="$work/token" TURBORAMA_SUITE_LICENSE_ID=TS-ADMIN-INTEGRATION dotnet "$root/src/TurboRamaPixOnlineServer/bin/Release/net8.0/TurboRamaPixOnlineServer.dll" >"$work/pix.log" 2>&1 & pix_pid=$!
sleep 2
[ "$(stat -c %a "$work/socket/admin.sock")" = 660 ]; token=$(tr -d '\n' < "$work/token")
[ "$(curl -s -o /dev/null -w '%{http_code}' --unix-socket "$work/socket/admin.sock" http://localhost/health)" = 404 ]
curl -s -D "$work/login.headers" -H 'Host: admin.test.local' -H 'X-Forwarded-Proto: https' http://127.0.0.1:55188/admin/login > "$work/login.html"
csrf=$(sed -n 's/.*name="__RequestVerificationToken" value="\([^"]*\)".*/\1/p' "$work/login.html");csrf_cookie=$(sed -n 's/^Set-Cookie: \([^;]*\).*/\1/p' "$work/login.headers"|tr -d '\r')
curl -s -D "$work/auth.headers" -o /dev/null -H "Cookie: $csrf_cookie" -H 'Host: admin.test.local' -H 'X-Forwarded-Proto: https' --data-urlencode "__RequestVerificationToken=$csrf" --data-urlencode 'username=turborama-admin' --data-urlencode "password=$admin_password" http://127.0.0.1:55188/admin/login
auth_cookie=$(sed -n 's/^Set-Cookie: \([^;]*\).*/\1/p' "$work/auth.headers"|tr -d '\r')
curl -s -D "$work/suite.headers" -H "Cookie: $auth_cookie" -H 'Host: admin.test.local' -H 'X-Forwarded-Proto: https' http://127.0.0.1:55188/admin/suite > "$work/suite.html"
rg -q 'TURBORAMA_SUITE' "$work/suite.html";rg -q 'Emitir OTP de 15 minutos' "$work/suite.html"
form_token=$(sed -n "s/.*name=__RequestVerificationToken value='\([^']*\)'.*/\1/p" "$work/suite.html");form_cookie=$(sed -n 's/^Set-Cookie: \([^;]*\).*/\1/p' "$work/suite.headers"|tr -d '\r')
post(){ curl -s -D "$1" -H "Cookie: $auth_cookie; $form_cookie" -H 'Host: admin.test.local' -H 'X-Forwarded-Proto: https' --data-urlencode "__RequestVerificationToken=$form_token" --data-urlencode licenseId=TS-ADMIN-INTEGRATION --data-urlencode deviceId=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa --data-urlencode confirmLicenseId=TS-ADMIN-INTEGRATION --data-urlencode confirmDeviceId=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa --data-urlencode "adminPassword=$2" -o "$3" http://127.0.0.1:55188/admin/suite/actions/issue-otp; }
post "$work/step.headers" invalid "$work/step.html"
[ "$(docker exec "$pg" psql -U postgres -Atc "select count(*) from suite.suite_audit_events where detail_code='STEP_UP_DENIED'")" = 1 ]
post "$work/issue.headers" "$admin_password" "$work/one-time.html"
! rg -qi 'onclick=|<script>' "$work/one-time.html";rg -q 'data-one-time-url' "$work/one-time.html"
post "$work/rate.headers" "$admin_password" "$work/rate.html";rg -q 'error=RATE_LIMIT' "$work/rate.headers"
[ "$(docker exec "$pg" psql -U postgres -Atc "select count(*) from suite.suite_audit_events where detail_code='RATE_LIMIT'")" = 1 ]
curl -s -H 'Host: admin.test.local' -H 'X-Forwarded-Proto: https' http://127.0.0.1:55188/admin/assets/admin.js > "$work/admin.js"
node "$root/tests/browser-suite-one-time.mjs" "$work/one-time.html" "$work/admin.js"
curl -s --unix-socket "$work/socket/admin.sock" -H "X-Suite-Admin-Token: $token" http://localhost/audit.csv > "$work/audit.csv"
rg -q '"[0-9]{4}-[0-9]{2}-[0-9]{2}T' "$work/audit.csv"
echo 'SUITE ADMIN INTEGRATION: OK (auth, CSRF/step-up, rate limit, audit CSV, socket, CSP/browser one-time)'
