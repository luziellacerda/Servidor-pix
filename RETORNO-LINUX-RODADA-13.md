# RETORNO LINUX - RODADA 13

- Início: 2026-08-14T12:03:00-03:00
- Término: 2026-08-14T12:32:52-03:00
- Hostname: lz-servidor-A520M-S2H
- Branch/commit: SEGURANCA-V2-LOCAL-20260814 / 82ecef33c883b38824582a63aeb60aee718ad606
- Commit funcional indicado: 92dba87
- Resultado: RODADA 13 APROVADA

## Exceção autorizada

O GitHub respondeu HTTP 200 anonimamente. O handoff determinava parar se o repositório estivesse público. A implantação prosseguiu somente após autorização explícita do responsável para ignorar essa proibição específica.

## Pacote

- TurboRamaPixOnlineServer-portable-SEGURANCA-R13-20260814.zip: 104839 bytes
- SHA-256 do ZIP: 1e1b49ee28cd9ae20bbdd24e5b53a6751e105e23859b794d108daf4fa63c8745
- Exatamente cinco arquivos, nenhum tipo proibido; manifesto integralmente OK
- DLL: 310784 bytes
- SHA-256 da DLL: fded6fbc4488254bf53542ddbdeeb17c9c2aa651d67bffadf697e0ac7bf0f6d9
- Self-test temporário e instalado como turborama-pix: código 0, SELF-TEST SERVIDOR ONLINE: OK

## Serviço antes/depois

- Antes: active/enabled, PID 380968, NRestarts=0, início 2026-08-14 11:33:44 -03
- Depois: active/enabled, PID 411182, NRestarts=0, início 2026-08-14 12:24:52 -03
- Usuário/grupo: turborama-pix:turborama-pix
- ExecStart: /usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll
- Diretório de trabalho: /opt/turborama-pix
- Listener antes/depois: somente 127.0.0.1:5187
- nginx, cloudflared e mariadb antes/depois: active/enabled
- Erros recentes ou posteriores: nenhum
- Espaço livre: 79 GiB; memória disponível: 11 GiB

| Arquivo anterior | Tamanho | SHA-256 |
|---|---:|---|
| CHECKSUMS-SHA256.txt | 432 | bb6dc4e84c6177632e6e535476d6f880e398d572e6711c3a9ee19a88518443f5 |
| TurboRamaPixOnlineServer.deps.json | 464 | ec658280d8716310532b713ea284c78f8c1dbbba0a3e5bcfb953ba8095cbadab |
| TurboRamaPixOnlineServer.dll | 249344 | 4c49b67a7ae719def39554b1064d71d0239f9b9bf5eb1c96bcff95b3644749a2 |
| TurboRamaPixOnlineServer.staticwebassets.endpoints.json | 53 | c1686417e8d5c31bba969f12b6f3d2b35e6609adb5ca42f212261a3a02771aac |
| TurboRamaPixOnlineServer.runtimeconfig.json | 536 | a9af57db55e6df5de551cd6ccc9d607872d87470124c3141916079f4f00b7f76 |

Arquivos posteriores: root:root, diretório 0755, arquivos 0644. A DLL instalada corresponde integralmente ao pacote R13.

## Backup e implantação

- Recuperação: /home/lz-servidor/backups/turborama-pix-r13-20260814-122451
- Manifesto: MANIFEST-FINAL-SHA256.txt
- SHA-256 do manifesto: 6ec282c9574af7bedc2bbdb4f57f4b4910b95af16b643c5fa6c4568d659fe995
- Aplicação, estado, ambiente, unit, Nginx e Cloudflare preservados separadamente e comparados: sim
- Serviço parado/iniciado: somente turborama-pix
- Troca: somente os cinco arquivos validados
- Rollback: NÃO APLICÁVEL

## Testes

| Teste | Antes | Depois |
|---|---:|---:|
| Health local 127.0.0.1:5187/v1/health | 200 | 200 |
| Health HTTPS público | 200 | 200 |
| Health HTTP público | 200 | 400 |
| https://pix.lzgames.com.br/admin | 404 | 404 |
| Painel pelo Cloudflare Access | 302 | 302 |
| Site da empresa | 200 | 200 |

- HSTS: Strict-Transport-Security: max-age=31536000
- Access preservado e nenhuma nova porta pública: confirmado

## Preservação

- Hash canônico comercial antes/depois: a44adea67800f3f0cecd8d8d24e1dee5345b5578f90637b2d00d8833b9bc5292
- Contagens antes/depois: 1 cliente, 1 licença, 1 máquina, 0 pagamentos, 14 eventos de auditoria
- Estado comercial alterado: não
- O hash bruto variou apenas por campos voláteis de sessão/último contato; clientes, licenças, máquinas, preços, credenciais e pagamentos permaneceram idênticos.
- Ambiente, unit, Nginx, Cloudflare Tunnel, banco/MariaDB e site alterados: não
- Porta 3306: mesmo mariadbd, PID 2258, antes/depois
- Portas 3302, 13306 e 23306: sem listener local antes/depois
- Pagamento, licença, código único, ativação ou credencial criados: não

Zero credenciais, senhas, tokens, cookies, chaves, conteúdo de .env, configuração secreta ou conteúdo comercial.
