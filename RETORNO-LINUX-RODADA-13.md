# RETORNO LINUX - RODADA 13

- Inicio: 2026-08-14T12:03:00-03:00
- Termino: 2026-08-14T12:32:52-03:00
- Hostname: lz-servidor-A520M-S2H
- Branch/commit: SEGURANCA-V2-LOCAL-20260814 / 82ecef33c883b38824582a63aeb60aee718ad606
- Commit funcional indicado: 92dba87
- Resultado: RODADA 13 APROVADA

## Excecao autorizada

O GitHub respondeu HTTP 200 anonimamente. O handoff determinava parar se o repositorio estivesse
publico. A implantacao prosseguiu somente apos autorizacao explicita do responsavel para ignorar essa
proibicao especifica.

## Pacote

- ZIP R13: 104839 bytes.
- SHA-256 do ZIP: `1e1b49ee28cd9ae20bbdd24e5b53a6751e105e23859b794d108daf4fa63c8745`.
- Exatamente cinco arquivos, nenhum tipo proibido; manifesto integralmente OK.
- DLL: 310784 bytes.
- SHA-256 da DLL: `fded6fbc4488254bf53542ddbdeeb17c9c2aa651d67bffadf697e0ac7bf0f6d9`.
- Self-test temporario e instalado como `turborama-pix`: codigo 0.

## Servico antes/depois

- Antes: active/enabled, PID 380968, NRestarts=0, inicio 2026-08-14 11:33:44 -03.
- Depois: active/enabled, PID 411182, NRestarts=0, inicio 2026-08-14 12:24:52 -03.
- Usuario/grupo: `turborama-pix:turborama-pix`.
- ExecStart: `/usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll`.
- Diretorio de trabalho: `/opt/turborama-pix`.
- Listener antes/depois: somente `127.0.0.1:5187`.
- nginx, cloudflared e mariadb antes/depois: active/enabled.
- Erros recentes ou posteriores: nenhum.
- Espaco livre: 79 GiB; memoria disponivel: 11 GiB.

## Backup e implantacao

- Recuperacao: `/home/lz-servidor/backups/turborama-pix-r13-20260814-122451`.
- Manifesto: `MANIFEST-FINAL-SHA256.txt`.
- SHA-256 do manifesto: `6ec282c9574af7bedc2bbdb4f57f4b4910b95af16b643c5fa6c4568d659fe995`.
- Aplicacao, estado, ambiente, unit, Nginx e Cloudflare preservados e comparados.
- Somente o servico `turborama-pix` foi parado e iniciado.
- Somente os cinco arquivos validados foram trocados.
- Rollback: nao aplicavel.

## Testes

| Teste | Antes | Depois |
|---|---:|---:|
| Health local 127.0.0.1:5187/v1/health | 200 | 200 |
| Health HTTPS publico | 200 | 200 |
| Health HTTP publico | 200 | 400 |
| https://pix.lzgames.com.br/admin | 404 | 404 |
| Painel pelo Cloudflare Access | 302 | 302 |
| Site da empresa | 200 | 200 |

- HSTS: `Strict-Transport-Security: max-age=31536000`.
- Access preservado e nenhuma nova porta publica: confirmado.

## Preservacao

- Hash canonico comercial antes/depois:
  `a44adea67800f3f0cecd8d8d24e1dee5345b5578f90637b2d00d8833b9bc5292`.
- Contagens antes/depois: 1 cliente, 1 licenca, 1 maquina, 0 pagamentos, 14 eventos de auditoria.
- O hash bruto variou somente por campos volateis de sessao/ultimo contato.
- Clientes, licencas, maquinas, precos, credenciais e pagamentos permaneceram identicos.
- Ambiente, unit, Nginx, Cloudflare Tunnel, banco/MariaDB e site nao foram alterados.
- Porta 3306: mesmo mariadbd, PID 2258, antes/depois.
- Portas 3302, 13306 e 23306: sem listener local antes/depois.
- Nenhum pagamento, licenca, codigo unico, ativacao ou credencial foi criado.
- Zero credenciais, senhas, tokens, cookies, chaves, conteudo de `.env`, configuracao secreta ou
  conteudo comercial no retorno.
