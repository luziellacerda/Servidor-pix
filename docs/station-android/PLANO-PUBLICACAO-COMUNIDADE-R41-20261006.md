# Publicação Station comunidade R41 — 06/10/2026

Pedido atual: ler e executar o handoff R41. Entrada do app: `cfa5ac2666d950fc1f8d9b58ec3e0a0c046a36e9`, recibo `a0dfb54`; delta do servidor publicado em `32b12bc5654b28f6dc73b9f5c2de2ef6a64bb616`, sobre `be2ba1f9c4d822c4c5d9731af375498d869184b2`. Este plano prepara a implantação; a prova de execução será registrada no retorno após a publicação.

## Alvo observado

| Campo | Produção antes da alteração |
|---|---|
| Unidade | `turborama-station-api.service` |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-relay-20261005-e4e557a/TurboRamaSuiteOnlineServer.dll` |
| Fonte da API | `e4e557a985ac5bead24885149c8650a5bb2dfae8` |
| DLL SHA256 | `7ecb6c8d94c5ab42c26638b5f9bdf7ffd0e70f99e047463ee5293af34f7bdcf5` |
| PID observado | `660598` |
| Catálogo | revisão14, 2.212 visíveis, 255 compatibilidade |
| Índice SHA256 | `07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92` |
| Registro dos motores SHA256 | `901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a` |
| Relay | habilitado, 512 salas /1.024 conexões; zero salas/conexões ativas observadas |

## Artefato e correção adicional

API .NET8 publicada em pasta imutável com `release.json`, revisão exata de fonte e hashes de todos os arquivos. O script confere Git limpo, artefato, unidade efetiva, configuração, índice, motores e licenças existentes antes de mudar produção. CRLF herdado nas duas fontes é normalizado para LF, preservando a configuração DI entregue.

O teste adicional reproduziu uma resposta de 544.672 bytes, acima dos 524.288 aceitos pelo Android, com 50 mensagens de sala, 32 privadas, página cheia, pedidos/convites máximos e 32 motores com Unicode. A API envia agora as mensagens privadas mais recentes dentro de 65.536 bytes de JSON. A fila continua com até32 mensagens; mensagens curtas continuam retornando32. O custo é calculado uma vez na aceitação; `encodedBytes` é interno e não aparece no contrato. A mesma amostra passou com envelope486.212 bytes. O ajuste altera somente a seleção do histórico privado enviado.

## Procedimento executável

`scripts/implantar-comunidade-station-r41-20261006.py --apply REVISAO_COMPLETA` exige autenticação administrativa nativa Linux. Usar `/mnt/DADOS/station-relay-check-20261005/venv/bin/python`, com `websockets==15.0.1`.

1. Backup privado da API atual e configurações; cópia da API restaurada com hashes conferidos. Dump PostgreSQL restaurado em cluster temporário, com ledger028/029/030 verificado.
2. Candidato em porta aleatória de loopback, usuário/grupo efetivos da Station, flag social desligada e ligada. Três licenças sintéticas independentes para privacidade, autoridade, sessão, assinatura e entrada após sair da própria sala. Regressão real do relay, catálogo completo, quatro capas simultâneas, download limitado de um jogo e revogação em long poll.
3. Somente novo drop-in `zzzzzzzzzzzzz-station-community-r41-20261006.conf` e reinício da unidade Station. Primeira ativação com social desligado, verificação, depois `Station__Online__SocialEnabled=true`.
4. Prova autenticada no domínio público: mensagem privada invisível ao terceiro, pedido/aceite/convite, edição divergente rejeitada, dois membros, ambos Pronto, Iniciar exclusivo do anfitrião, host-listening e bytes reais por WSS com pin TLS existente.
5. Remover apenas fixtures/salas de teste; comparar hashes e PIDs dos demais serviços e linhas das licenças de clientes. Não aplicar migrations nem alterar Nginx, Cloudflare, portas, chaves, scanner, mídias, painel ou outras aplicações.

Salas/mensagens são efêmeras e são perdidas no reinício da API. O script recusa reiniciar se houver partida com conexões de relay ativas. A capacidade configurada e os testes sintéticos não certificam gameplay em centenas de Android.

## Retorno

`--disable-social REVISAO_COMPLETA` mantém a release nova e salas/convites/relay anteriores, desligando a extensão social. `--rollback REVISAO_COMPLETA` remove somente o drop-in desta publicação e retorna à API `e4e557a`; ambos conferem a versão efetiva e recusam uma sucessora. Falha após ativação provoca tentativa automática desse retorno. Backup e configs são privados; não restaurar o dump sobre o banco vivo para desfazer uma mudança sem migration.

APK R41 candidato: SHA256 `b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d`. Não instalado segundo o handoff; R39 é a última instalação comprovada. Instalação R41 com assinatura original, preservação dos dados e gameplay real em dois aparelhos continuam no retorno para o operador do app. Não instalar delta R34 sobre R41. Não há autorização para envio externo de WhatsApp/MenuIA.
