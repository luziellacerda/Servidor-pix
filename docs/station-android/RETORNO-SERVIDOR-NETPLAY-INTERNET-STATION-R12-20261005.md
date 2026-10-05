# Retorno de produção — relay R12, POCO e capacidade — 05/10/2026

## Estado atual

**Relay privado publicado no domínio Station.** Configuração para **512 salas / 1.024 conexões**, dois jogadores por sala. Passaram 256 conexões reais em 128 salas e 512 conexões em teste TLS isolado, com dados ordenados, sem corrupção/perda e zero resíduos após encerramento.

**Latência pública baixa permanece pendente.** Com 256 conexões, p95 daAPI 0,83 ms eNginx local 1,07 ms; WSS público2.291,82ms. Uma sala pública também apresentou p95 de 2.176,15 ms. Capacidade do backend foi exercitada; partidas responsivas pela internet e gameplay de dois Android ainda precisam de homologação. O ensaio público saiu do próprioLinux e atravessou o domínio externo; não representa diretamente as redes dos telefones nem identifica sozinho operadora, rotaWAN ou túnel como causa exclusiva.

[Evidência sanitizada](online-poco-20261005/evidencia-producao-capacidade.json). Este é o retorno solicitado pelo [handoff do app R12](HANDOFF-APP-SERVIDOR-NETPLAY-INTERNET-R12-20261005.md).

## Versão publicada

| Campo | Verificado |
| --- | --- |
| Serviço/PID | `turborama-station-api.service` /660598 |
| Fonte do binário | `e4e557a985ac5bead24885149c8650a5bb2dfae8` |
| DLL SHA256 | `7ecb6c8d94c5ab42c26638b5f9bdf7ffd0e70f99e047463ee5293af34f7bdcf5` |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-relay-20261005-e4e557a/TurboRamaSuiteOnlineServer.dll` |
| URL/porta | `https://app.lzgames.com.br` /443 |
| Upstream | `http://127.0.0.1:5192` |
| Relay | `wss://app.lzgames.com.br/v1/station/online/relay` |
| Catálogo | Revisão14 /2.212visíveis /255compatíveis /2.467internos |
| Índice SHA256 | `07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92` |
| Registro motores SHA256 | `901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a` |
| Migrations desta entrega | Zero; ledger028/029/030 existente |

API931030b e referências de 04/10 são históricas. Catálogo, IDs, mídias, chaves, scanner, importação automática e capas da revista foram preservados. SNES644/SNESBR191/Mega887/MegaBR94/N64157/NeoGeo189/CD50. ProvaHTTPS comparou os2.212IDs, metadados e `folderPath`, quatro capas concorrentes, download real de 262.144 bytes e concessão de uso único. **Downloads de jogos continuam sem limitador artificial de bytes/s.**

## Licença pronta para o POCO

- ID de teste: `STA-7367E11ED0124C829FAC9F6CFDCB8B49`.
- Nome no painel: **Teste POCO - segundo aparelho online**.
- Produto/aplicação: `TURBORAMA_STATION_ANDROID`; vitalícia, **um aparelho ativo**. Licença do telefone original preservada.
- Código emitido uma vez, geração 1. Primeira ativação até **07/10/2026 às17h11min33s, America/Maceio**. Esse prazo pertence ao código; a licença é vitalícia.
- Conferência final: ACTIVE/PENDING_ENROLLMENT, código válido, zero aparelhos vinculados. Nenhum teste consumiu a ativação POCO.
- Código apenas no arquivo privado `/home/lz-servidor/LICENCA-POCO-STATION-20261005.txt`, modo 0600. Código/tokens/chaves não acompanham o Git.
- Concessão administrativa de homologação auditada: `STATION_TEST_LICENSE_GRANTED`, origem `STATION_OPERATOR_TEST`. Nenhuma venda, cobrança ou notificação comercial criada. Campos técnicos de elegibilidade permitem o teste.

**Uso humano:** instalar a versão atual no POCO, abrir Station e colar o código privado na ativação. O app vincula a chave Keystore do telefone e renova a sessão nas próximas aberturas.

Suporte: [Administração Station](https://turbobox.lzgames.com.br/admin/station), buscar nome/ID acima. **Gerar novo código** reemite por 30 minutos e invalida o anterior. Troca de telefone/reinstalação usa a transferência/liberação dessa licença específica; bloqueio/liberação pelas ações auditadas. Atualizações preservam dados e identidade.

## Aplicativo recebido e roteiro dos dois telefones

Retorno `4fd2231045142a3269c479f75fa8a0e8388cc403`: **R27 instalado** em 05/10 às 18h02, hash integral do telefone conferido:

```text
G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Salas-Lista-R27-20261005.apk
SHA256 c1191ce1d531b1ec9171d6d4c2f57541349339e64e8941a33788c4067d616bd6
```

R27 preserva carrossel/LED/layout R26 e túnel WSS R12; acrescenta lista compacta, perfil, convites e códigoTS1. Só classes35 mudou/13.083 entradas preservadas. Tela bloqueou após instalação: visual/sessão nessa revisão e gameplay de dois aparelhos ainda precisam de prova. Delta de downloads11be7f3/6f012a7 e ponteCD são entregas separadas, ainda fora do APK27. NenhumAPK foi instalado neste Linux.

1. Usar R27 ou sucessora comprovada nos dois telefones, certificado original e atualização com dados. Cada aparelho usa sua licença; o POCO recebe a licença acima.
2. Baixar a mesma edição de um jogo SNES/Mega. Exemplo real: **Super Mario Kart**, `snes`, item `station_c163bc17b96efcad31302280d44fb819`, revisão4, jogadores1–2. Preparar a mesma ROM/core/runtime/opções pelo app.
3. Abrir **Jogar online → Reconectar** depois desta publicação. Criar sala para dois jogadores.
4. Convidado entra pelo convite ou copia/cola **Código da sala** R27. TS1 é localizador público `instance/roomId/itemId`; entrada permanece autenticada. Reinício muda `instance`, exigindo sala/código novos.
5. Ambos marcam **Pronto**; anfitrião inicia pela internet. Cada telefone abre seu WSS para Station; sem configurar IP público ou porta de roteador do jogador.
6. Conferir ações dos dois controles, sincronismo, áudio, duração, saída conjunta, saves e retorno às salas/plataformas com sessão mantida.
7. Repetir Wi-Fi+rede móvel e depois dois Wi-Fi distintos. Registrar versão, rede, jogo, horário, atraso percebido e erros sem códigos/tokens.

Online liberado: **SNES/SNESBR eMega/MegaBR**, `bsnes-mercury-performance-79d7f9de` e `clownmdemu-d43c2708`, hashes preservados. N64/NeoGeo/CD e demais motores mantêm sua disponibilidade local; não foram liberados para netplay.

## Leitura correta do servidor pelo app

Desafio/prova RSA do Keystore, assinatura do servidor, produto/aplicação/licença/aparelho e sessão continuam no contrato Station instalado. Catálogo/capas/downloads usam autorização eIDs realmente retornados.

| Operação | Forma correta |
| --- | --- |
| Presença/capacidades | POST `/v1/station/online/command`, `enter`/`heartbeat`, Bearer próprio |
| Alterações | POST `/v1/station/online/events`, mesmo Bearer, `instance/revision/page` e novo requestId |
| Sala | `create`, `join`, `ready`, `start`, transporte `relay-wss-v1` anunciado no snapshot |
| Ticket | `relay-ticket`; resposta assinada em `snapshot.room.relay` do participante |
| Transporte | GET/upgrade em `relay.path`; `station-relay.v1`; `Authorization: StationRelay <ticket>` |
| Host pronto | `host-listening` após callback real do runtime; geração preservada no relay |
| Saída | `leave` encerra participação na sala; `offline` remove presença |

`requestId`: UUID formato D com hifens. Verificar assinatura/domínio/sessão/licença/aparelho/requestId. Status200/101 sozinho não comprova gameplay. Ticket de 43 caracteres, 60 s e uso único, vínculo participante/sala/geração; nova emissão substitui o anterior. Valor só no header/memória; URL sem query. Mensagens binárias ordenadas. BASE_URL/hostname/truststore/pin/chaves existentes preservados.

Heartbeat20s, expiração da presença60s, watch2s, pareamento60s e sessão180s. Poll pendente revalida a autenticação após a espera e revoga o hub. Alteração isolada na base exige novas autenticações/heartbeats ou expiração da presença; não promete encerramento instantâneo. Saída/falha de um lado fecha os dois sockets e libera a sala. Reinício/troca de conexão encerra a partida; nova sala para reconectar.

## Correções e configuração de capacidade

Efetivas apenas na unidade Station:

```ini
Station__Online__Enabled=true
Station__Online__RelayEnabled=true
Station__Online__RelayMaxRooms=512
Station__OriginRequestsPerMinute=2048
Station__MaximumRateWindows=32768
Station__LibraryVerifyContentOnLoad=false
```

- **Limpeza concorrente:** Abort podia acessar HttpContext descartado antes de liberar um slot. Limpeza agora em finally obrigatório; cancelamento/IO fora do lock global.512 TLS locais/256 reais terminaram com zero slots de teste.
- **Sessões:** SERIALIZABLE gerava conflitos entre licenças diferentes. Abertura usa READ COMMITTED, `FOR UPDATE` da licença até commit, revalidação de geração/elegibilidade, consumo condicional do desafio e substituição da sessão ativa. RSA, uso único e revogação preservados; ativação mantém regras anteriores.64 renovações / 16 workers, replay concorrente, sessão única e revogação durante lock passaram no PostgreSQL isolado com papel API;256 renovações públicas passaram.
- **Origem/NAT:** controle ampliado30 → 2.048 requisições/minuto por rota e32.768 janelas nesta API. Defaults dos demais serviços/produtos e budgets covers/dispositivo preservados. Esse controle é de requisições, sem cap deMB/s dos jogos.
- **Coldboot:** índice/conteúdos já validados pelo importador privilegiado evitam rehash integral repetido. Schema/paths/existência/tamanho/mtime/snapshot/last-good/grants permanecem. Default da flag true; só a unidade Station a desliga. Importador confere novos arquivos. Candidato final iniciou em 1,39 s.
- **Handshake:** recusa WS passou a ter JSON finito/Content-Length. Antes,401 da API virou 502 externo; após ajuste,401/400 corretos e101 válido passaram. Retorno da tentativa anterior concluído, evidências mantidas.
- Buffers/filas limitados e escrita aguardada preservam ordem/backpressure.8 MiB/s por direção/sala do relay aplica-se ao netplay; downloads de jogos permanecem sem cap. Emulação ocorre nos telefones. Uma instância autoritativa; réplicas precisam de estado/afinidade próprios.

## Medições e pendência externa

Gerador: frequência alvo 60 mensagens/s/direção/sala,32 bytes, 1.200 roundtrips/par. Essa frequência não mede FPS da emulação Android.

| Caminho | Conexões/salas | Roundtrips | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: | ---: |
| TLS isolado |512/256|307.200|0,75ms|1,16ms|1,82ms|
| API real local |256/128|153.600|0,56ms|0,83ms|1,16ms|
| Nginx real local |256/128|153.600|0,76ms|1,07ms|1,54ms|
| WSS público C# |256/128|153.600|940,84ms|2.291,82ms|2.721,87ms|
| WSS público C# |2/1|1.200|260,34ms|2.176,15ms|2.424,68ms|

Sem perdas/corrupção; API ready na carga; contas sintéticas removidas e licenças/serviços compartilhados preservados. Público256: pico API 631.533.568 bytes, gerador 136.798.208 bytes. CPU doTLS isolado inclui cliente/servidor no mesmo processo.

Primeiro gerador Python também completou 256 conexões, p95 1.342 ms. O C# foi usado para investigar a espera: não atribuímos o problema apenas ao Python. API/Nginx rápidos com a mesma carga delimitam o atraso à etapa externa após o proxy. NIC 100 Mbps/full, zero erros/drops observados; placa e parceiro anunciam 1000 Mbps. Isso limita banda agregada, mas não prova a causa da latência de uma sala. Nenhuma alteração global de rede/Cloudflare foi executada.

**Para homologar acesso público:** comparar o roteiro dos dois telefones/redes e investigar trajeto WAN/túnel, mantendo controle API/Nginx e métricas agregadas. Conferir também cabo/porta que negociam 100 Mbps sem interromper os demais produtos. Corrigir a espera externa antes de anunciar gameplay responsivo para centenas; aumentar o teto novamente não corrige essa espera.

## Operação e retorno exato

Telemetria agregada apenas loopback: `GET http://127.0.0.1:5192/ready/station/online`, `maximumRooms/maximumConnections/activeRooms/activeConnections/forwardedBytes`. Sem lista de usuários/tickets. Pós-teste: zero salas/conexões e zero linhas temporárias; concessão POCO permanece.

Proxy: `/etc/nginx/snippets/turborama-station-relay.locations.conf`, incluído pelo online existente. HTTP1.1/Upgrade/Connection/subprotocolo/Authorization preservados, timeout 120 s, cache/buffering/accesslog desativados nessa rota. Sem porta pública de emulador, DNS, pin/chave ou regra global nova.

Banco/API anteriores restaurados e verificados antes da publicação: `/mnt/DADOS/station-relay-backup-20261005-0bf8a0e`. Backup próprio da correção de sessões: `/mnt/DADOS/station-relay-session-update-backup-20261005-e4e557a`.

Retorno atual, com autenticação nativa do operador:

```sh
/mnt/DADOS/station-relay-check-20261005/venv/bin/python \
  /mnt/DADOS/servidor-pix-station-poco-online-20261005/docs/station-android/scripts/atualizar-sessoes-relay-station-20261005.py \
  --rollback e4e557a985ac5bead24885149c8650a5bb2dfae8
```

Restaura 0bf8a0e/relay publicado, retornando também ao conflito de sessões sob carga. Recusa versão/configuração posterior. Reinicia Station e encerra salas; POCO/banco/catálogo/mídia/scanner/chaves preservados. Rollback da primeira publicação é histórico e recusa API e4e557a; scripts antigos de04/10 não devem ser aplicados a esse estado.

[Script de carga](scripts/carga-relay-station-20261005.py): compilar `tests/StationRelayPublicLoad` antes de executar. Tickets por pipe anônimo, sem arquivo/log. Fixtures com marcador próprio e limpeza transacional. Padrão 128 pares/público; diagnóstico `--pairs 1`, `--transport api|proxy`. Recibo existente impede repetição cega: preservar evidência e comprovar limpeza antes de nova rodada.

Backend e licença disponíveis para a homologação. **Latência pública baixa e partida real de dois Android permanecem critérios abertos.**
