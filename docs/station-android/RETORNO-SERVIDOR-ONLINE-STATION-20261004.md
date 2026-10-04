# Servidor → aplicativo: salas Station publicadas, 04/10/2026

Resposta ao [handoff APP → servidor](HANDOFF-APP-PARA-SERVIDOR-ONLINE-20261004.md), referências do app **dd6aff172761f40c9b8eea2d3e2c1c34a0c79034** e servidor recebido **308fda4124439110a34db9beba689c6f96535b39 / b16b73a1860d97af8e7748b53484da804522b5dd**. Estado conferido até **11h32 America/Maceio**. O mantenedor solicitou concluir todas as etapas do handoff antes de adicionar N64.

**Publicado às11h29:** duas rotas online habilitadas em HTTPS, testes autenticados com duas licenças sintéticas passaram; catálogo revisão4/1.816, revista e downloads preservados. **R7 continua candidato compilado, sem instalação recebida neste Linux; partida P2P entre aparelhos continua pendente.** O [handoff operacional único](RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md) reúne o estado geral. Este arquivo responde especificamente ONL-01 a ONL-07.

## ONL-01 — revisão e serviço efetivo

Base recebida **54bba11c52f35695fd47eabc7145f42af9990426**, compatível com API anterior4bb77ed2. Worktree próprio `/mnt/DADOS/servidor-pix-station-online-review-20261004`, branch `review/station-online-handoff-20261004`; outros worktrees preservados.

- Fonte da DLL publicada: **77d1dfb50a9982b01d8d649db477e6268dc7a5fb**. Ferramentas de implantação: **977989396f0bbb9a4f64d17eaae2e7619cc9a2aa**.
- Serviço **turborama-station-api.service**, ativo, **PID321167**, UID995/GID981. ExecStart: `/usr/bin/dotnet /opt/turborama-station-online-20261004-77d1dfb/TurboRamaSuiteOnlineServer.dll`.
- DLL SHA256 **ff6362852635d4d18a01e85e46c89ad5cc2a7dad75d3b99793a124733beb6509**, conferido no arquivo executado após implantação.
- Drop-ins anteriores rev3, rev4-covers e speed preservados; acrescentado **zzzz-station-online-20261004.conf**, depois deles na ordem efetiva. WorkingDirectory da nova release; flag e registro definidos somente nessa unit.
- Índice efetivo `/mnt/DADOS/turbostation-releases/station-revista-20261003-rev4/content/index.json`, SHA256 **c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a**. Nenhum item/ROM/capa/descritor foi alterado pelo módulo.
- Ledger028/029/030 conferido; **nenhuma migration nova**. Ready local e HTTPS200. PIX/Suite/admin/gateway e dependências ativos; os12 PIDs acompanhados permaneceram iguais.

## ONL-02 — testes e correções

Reproduzidos os **39 checks de domínio +37 HTTP** entregues. Revisão corrigiu duas validações: página inválida agora falha antes de reservar/esperar no poll; campos JSON duplicados são recusados nas duas rotas. Contrato de campos opcionais e assinatura preservado. Após correções: **41 domínio +41 HTTP**.

PostgreSQL temporário: **92 checks online com duas ativações e sessões reais sintéticas**, mais regressão de ativação, perfil/catálogo/grant assinados,48 capas com quatro pedidos simultâneos, raw/ZIP/hash, interrupção/novo grant, uso único, outro aparelho/sessão, expiração por registro e revogação. Quatro plataformas e um ID oculto antigos conferidos. A mesma DLL empacotada foi executada com online ligado e desligado. A primeira tentativa de dois clusters em paralelo encontrou conflito de porta; a execução desligada repetida sequencialmente passou. Banco real não foi usado nesses testes.

Suite unitária e contrato ES em memória passaram. A suíte SQL própria de ES não foi executada sem conexão isolada específica; não atribuir a ela a prova SQL de Station. Não foram testados compradores reais ou pagamento real.

```bash
dotnet run --project tests/StationOnline/StationOnline.Tests.csproj --configuration Release
dotnet run --project tests/StationOnline/HttpTests.csproj --configuration Release
STATION_HTTP_ONLINE_CHECKS=1 pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
STATION_HTTP_ONLINE_CHECKS=0 pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

Para testar o artefato, acrescentar `STATION_HTTP_API_DLL` com a DLL empacotada. `STATION_HTTP_EXTRA_INDEX` é opcional e privado; não publicar seus caminhos de mídia. O teste recusa bancos fora de `/tmp/pg_virtualenv.*`.

## ONL-03 — flag e motores

Flag desligada foi comprovada no candidato sob UID995: ambas as rotas503 **STATION_ONLINE_DISABLED**. Os testes Kestrel também comprovam esse estado sem dependências do banco. Depois da homologação, produção passou a **Station__Online__Enabled=true**.

Registro efetivo **/opt/turborama-station-online-20261004-77d1dfb/online-engine-registry.json**, SHA256 **901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a**, propriedade root/GID981, arquivo640, release750. Leitura pelo usuário real do serviço comprovada. Comparação semântica exata com `server-contract/engine-registry-candidate.json` e os motores `launchReady=true` de `assets/station-online/engines.json` no app dd6aff1.

Motores permitidos: **bsnes-mercury-performance-79d7f9de / snes** e **clownmdemu-d43c2708 / megadrive**. Hashes do core/runtime são os recebidos no APK. Neo Geo não registrado; CPS/MAME/FBNeo não foram acrescentados. TLS pin, autoridade de assinatura, produto, licença, sessão e regras comerciais permanecem iguais.

## ONL-04 — proxy

Acrescentado snippet **/etc/nginx/snippets/turborama-station-online.locations.conf**, incluído pelo snippet Station anterior. Exatamente os dois POSTs:

- `/v1/station/online/command`
- `/v1/station/online/events`

[Candidato versionado](ops/nginx-v1-station-online.conf): timeout30s de envio/leitura para deadline15s/poll10s; corpo8KiB, sem buffering/cache/log de acesso; Bearer e X-Correlation-ID encaminhados. Cookie/Proxy removidos. Nenhuma rota Suite, artefato, capa, TLS, túnel ou firewall alterada. Syntax isolada e `nginx -t` real passaram; só recarga Nginx, sem troca de seu PID principal. O prefixo Station anterior tinha15s; o acréscimo deu margem às salas sem modificar seus demais endpoints.

## ONL-05 — homologação autenticada e reinício

Candidato sob UID995 e produção HTTPS passaram **92 checks cada**: duas licenças sintéticas distintas, sessão real/Postgres, assinatura RSA-PSS e vínculos de licença/aparelho/sessão/requestId, privacidade de senha/chat, catálogo, convite fora da página, repetição idempotente, hashes divergentes recusados, dois jogadores prontos, starting→host-listening→connecting, um poll por aparelho, revogação durante espera, saída e limpeza.

Prova adicional: dois processos candidatos sequenciais da **mesma DLL**, sem reiniciar novamente a API pública. Após reinício, instance mudou, presença/sala antiga desapareceu, heartbeat antigo recebeu409 ENTER_REQUIRED, a sessão existente continuou válida para perfil e Reconectar recriou presença; cursor do processo anterior devolveu a nova instance. Todos os registros sintéticos removidos; total de deliveries **STATION_ROLLOUT_TEST=0** conferido. Nenhuma licença de comprador ou mensagem externa usada.

`connecting` não comprova transporte de inputs ou sincronismo RetroArch. A API social não hospeda ROMs nem relay e não valida reachability do IP do jogador. Homologar a partida em dois aparelhos é a etapa Android restante.

## ONL-06 — implantação, backup e retorno

**Implantado**, não apenas Git. Artefato candidato `/mnt/DADOS/station-api-online-candidate-20261004-77d1dfb`; manifesto SHA256 de13 arquivos mais `release.json`. Release instalada root/grupo do serviço, sem escrita da API.

Backup privado root0700 **/mnt/DADOS/station-online-backup-20261004**: API4bb77ed2 anterior completa, configurações da unit, proxy anterior e resultados. API salva foi restaurada em pasta temporária e todos os hashes comparados antes de ativar. Binário anterior continua SHA256 **b08f8313651a10de008d35545ff13569c5a360fb42ee792ad37f2647bd9d107e**.

Primeiro preparo parou antes de trocar a API por usar criação exclusiva em arquivo proxy existente; substituição atômica corrigida e testada. A tentativa seguinte detectou que o override estava antes do speed e **retornou automaticamente**. Arquivos/PIDs/índice antigos conferidos. Ajustada ordem `zzzz`, com checagem do ExecStart após daemon-reload antes de restart. Uma tentativa de retomada sem commit limpo foi recusada; leitura Git passou a não atualizar índice sob root. Os recibos de falha foram preservados. Retomada final em9779893 concluiu todas as provas.

**Retorno somente deste módulo**, com autenticação administrativa nativa, na raiz de um checkout contendo as ferramentas9779893:

```bash
python3 docs/station-android/scripts/implantar-online-station-20261004.py --rollback
```

Confere DLL/config/índice, remove só o override online, restaura proxy anterior, remove o snippet próprio, valida/recarrega Nginx e reinicia apenas Station para4bb77ed2. Mantém catálogo/capas4, chaves, licenças, schema, backups e releases. Salas efêmeras são encerradas; código anterior não tem essas rotas e o app exibe indisponibilidade. O procedimento recusa estado posterior divergente: depois de outra implantação, usar o retorno desse novo estado. **Não repetir --apply/--resume sobre a publicação concluída.**

## ONL-07 — HTTPS e ação do aplicativo

Base permanece **https://app.lzgames.com.br**. Dois POSTs sem Bearer receberam **401 STATION_SESSION_INVALID**, no-store/nosniff e eco da correlação. Com sessões sintéticas reais,200 com envelopes e contexto assinados; prova de operações no item anterior. Antes da publicação as duas rotas eram404; isso não identificava falha do login/catálogo/capas.

O **APK R7 827723436ac618d3b1745a873813c7781ff10e043abc6033de416c01d774703d** pode consumir as rotas implementadas no seu próprio código, usando `StationSessions.Lease` e o Bearer existente. Não usar código STA nem criar outra sessão no processo nativo. Presença usa enter/heartbeat/eventos; salas têm hashes do jogo instalado/core/runtime/opções; cliente só abre motor após confirmação do host. Não enviar senha/endereço/credenciais aos logs.

Última instalação recebida: **R4 17e9b87b**, com1.816 jogos/8 instalados, capas visíveis/cache persistente/quatro workers e sinopses observadas. R7 está compilado/assinado, **não instalado**; sem USB/segundo aparelho neste Linux. Atualizar por instalação sobreposta com assinatura original, preservando Keystore/licença/jogos/saves/motores; conferir vídeo/visual/capas/downloads e depois duas licenças em Wi-Fi/rede externa alcançável. Não promover a estável só pelas provas do servidor. Neo Geo/CPS permanecem pendentes.

[Evidência sanitizada completa](online-20261004/evidencia-publicacao-linux.json). Nenhum segredo ou dado de comprador incluído. O pedido seguinte de mover/adicionar N64 e automatizar leitura de pastas começa depois deste retorno; **N64 ainda não faz parte do catálogo nesta fotografia**.
