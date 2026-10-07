# SERVIDOR → APP — implementação candidata de retomada após R67/R68

07/10/2026. **Código, contrato, DEX/runtime e testes isolados entregues. Não implantado na API, não empacotado/instalado no Android, nem homologado em gameplay de dois aparelhos.** A causa específica da queda antiga continua desconhecida. A R68 visual recebida durante esta preparação foi incorporada; não restaurar o APK R67 sobre a R68 instalada.

## 1. Fontes e estado de cada entrega

| Componente | Identidade / estado |
|---|---|
| Pedido recebido | Servidor-pix `6c5f5cf33cf3ee9930066d52a55fa3357de75c13`, branch docs/station-r67-recovery-implementation-20261007; [pedido REC-01–09](PEDIDO-IMPLEMENTACAO-RETOMADA-ONLINE-APOS-APP-R67-20261007.md) |
| APP R67 lido | TurboElden fonte `0d7a44f371e846a9821426a9082405836e250b0e`, entrega `589d678612880c10e40ef42d23e73451ac2397d9`, pedido `319a74150213d525f06b7a16fb234294bfd83edb` |
| Sucessora recebida R68 | TurboElden `c2a1a6d130552b4470b2be3f49db0d1ab72c37b5`, fix/station-collection-corners-r68-20261007; instalação Samsung07/10às19:32:25UTC, recebida nos [recibos exatos](entrega-app-r68-20261007/ORIGEM.json) |
| Implementação servidor | **`32ce9d2b30bb23deef17899e10fc285f38ea81ab`**, branch **fix/station-online-recovery-r67-20261007**; [fonte](https://github.com/luziellacerda/Servidor-pix/tree/32ce9d2b30bb23deef17899e10fc285f38ea81ab) |
| Implementação Android/runtime | **`4d30401a80658dd56666ef10f48d9556b3fdd9e9`**, branch **fix/station-online-recovery-after-r67-20261007**, pai R68c2a1a6d; [snapshot/receitas](https://github.com/luziellacerda/TurboElden/tree/4d30401a80658dd56666ef10f48d9556b3fdd9e9/versions/station-online-recovery-r67-20261007) |
| DLL candidata local | `0c0c0b48bc60dc321ef8026fc2a03e9e992993b6b80f5a8d768851d09d3ace7b`; dotnet publish Release, sem instalação; [recibo](recovery-r67-20261007/server-tests.json) |
| Android montado/assinado/instalado | **Pendente**; os dois DEX e runtime estão compilados, não há novo APK desta recuperação |
| Produção ativa | Fonte publicada anteriormente da07355, PID1147382/usuário turborama-station-api, [observação atual](recovery-r67-20261007/production-observation.json); não recebeu o candidato |

A R68 só altera libturbo_carousel.so; conserva todos os DEX/runtime/core/segurança R67. Fonte Java compilada continua a composição R67 exata, acrescida da recuperação. O snapshot novo resolve193 arquivos da R67 e totaliza195 com duas classes novas. Não copiar a Activity/empacotador R57. Nove overrides preservam os componentes de salas/controles/Binder conciliados e a BIOS do DEX30. **A nova receita exige APK R68**, compara core original, certificado e todas as entradas preservadas, inclusive55vídeos e cantos retos de todas as coleções.

APK instalado R68: `72ce7c2cbb3d7cf14ad122b0b98e42a41563114bbbc5c925caedbdee49ff66c3`/2.110.287.488 bytes. Samsung A56 observado na ESActivity/coleções SNES, UID/data original preservados pelo implementador Android. Segurança R67 permanece, mas o recibo R68 não prova nova autenticação/atestação ou partida. Outro telefone: última instalação recebida R63. Não declarar ambos atualizados.

## 2. Contrato executável e authority correta

- [OpenAPI3.1 e schemas](recovery-r67-20261007/openapi.json), [contrato completo](recovery-r67-20261007/CONTRATO.md), [vetores binários/assinados](recovery-r67-20261007/contract-vectors.json), [configuração desativada por padrão](recovery-r67-20261007/configuration.example.json).
- API real do Android: **`https://app.lzgames.com.br`**; WSS **`wss://app.lzgames.com.br/v1/station/online/relay`**. Preservar authority e pin atuais. **`turbobox.lzgames.com.br` é site/painel**; tentar `/v1/station/catalog` nele retornou404. A rota no host do app retornou401 JSON `STATION_SESSION_INVALID` sem credencial, esperado.
- REST continua `/v1/station/online/command` e `/events`, envelope assinado online/v1. Não inventamos rotas `/v2`.
- Negociação explícita `recoveryCapabilities`, engine e ambos membros `recoveryProtocol:"station-stream.v2"`; transporte `relay-wss-v2`/subprotocolo WSS `station-stream.v2`. Clientes antigos continuam v1; pares de engines/runtimes/protocolos diferentes são recusados.
- `resume-relay` exige roomId/generation/engine/quatro hashes, sessão protegida e prova; novo ticket individual de uso único60s e prova fresca no upgrade. TLS/pin/nonce/assinatura/licença/aparelho/edição são mantidos. O corpo é controle, não ROM; não houve verificação acrescentada ao download nem releitura da ROM a cada reconexão.
- Estratégia implementada: manter TCP/motor em RAM, sequenciar/deduplicar os bytes e trocar apenas WSS. Header TSR2 de24bytes/opcodes1–13, payload≤16KiB, rings fixos, ACK após escrita real no TCP, reconciliação de ACK perdido e barreira dos dois participantes. Não é reenvio cego nem ressurreição persistida após processo morto.

## 3. Resposta REC-01–09 / Q01–Q08

| Pedido | Implementação entregue / prova / limite |
|---|---|
| REC-01, Q01 | `StationRelay.First` registra primeiro evento v1 antes do cancelamento conjunto; `StationRecoveryRelay.First` por attachment/época registra UTC/correlação/geração/função/categoria/close/exceptionTYPE/idades/contadores por direção. Controle `StationOnlineTrace` registra modo de prova e duração interna. Java correlaciona estado/close/type/RTT. Sem tickets/tokens/mensagens/identidade pessoal. Novo diagnóstico ainda não executou na produção/Android; stacktrace JNI sanitizado e causa antiga pendentes. |
| REC-02, Q02 | V2 separa sala/membro/geração de attachment. Sweep expira presença social, conserva partida v2 mesmo ambos ausentes; não transforma estado irrecuperável em recuperável. Detach cancela somente seu WSS e preserva rings/TCP. Membership>60/120s exercitada com relógio sintético180s; salas aguardam sem despejo por idade, sujeitas a slots limitados. |
| REC-03, Q03 | Contrato/nonce/prova protegida, `resume-relay`, attachment novo e credenciais únicas. Gen/core/runtime/ROM/opções/protocolo vinculados; ligação concorrente recusada e finalizador antigo não derruba a nova. Vetores públicos .NET/Java e TLS real validaram proteção; ticket sem prova401 não consumiu a credencial. Hardware/atestação real não comprovados. |
| REC-04, Q04 | Offsets por direção, accepted/delivered separados, replay limitado e comparado, ACK perdido via HELLO, dedup, backpressure/rings. Java/.NET confirmou1.620.000 bytes mantendo os dois TCP após perda host/convidado/ambos e retorno. Confirmação de jogo/frame/determinismo Android ainda pendente. |
| REC-05, Q05 | Hooks JNI `stationRecoveryControl/Status/Stalled`; pedido versus confirmação no thread real. Runloop antes do core_run pausa motor, processa netplay sem executar core; NEED_SYNC cobre stall sem fechar WSS. Dois patches históricos + patch novo/receita/source hashes; runtime/IDs novos, registro aditivo. ELF e13invariantes host aprovados. JNI/motores SNES/Mega ainda não executados fisicamente. Não habilita NeoGeo/CD online por inferência. |
| REC-06, Q06 | Activity oculta suspende attachment e solicita pausa; main process mantém renovação/heartbeat20s enquanto vivo; volta faz credencial/barreira sem outro motor. Java tick250ms/ping10s/backoff até10s, rings limitados. Process kill/backend restart explicitamente irrecuperável; não persistimos savestate. Consumo/energia/temperatura real pendente. |
| REC-07, Q07 | Sessão expirada usa renovação existente, indisponibilidade temporária estaciona ligação, licença/device negados mostram terminal e revogação remove autorização. Watch físico revalida acesso, não ignora eternamente heartbeat/IO. Nenhuma mudança em Cloudflare/Nginx/produção; serviço efetivo e API correta conferidos, configuração de proxy em disco registrada com limite de evidência. |
| REC-08, Q06 | UI “Aguardando conexão…”, indicador indeterminado/baixo custo, “Sair da partida” com confirmação, `synchronizing` antes de playing. Sem prazo/porcentagem/Leave por simples rede. Estado nativo perdido exige explicação/saída humana. Activity/visual só compilados; toque/layout real pendentes. |
| REC-09, Q08 | Código e branches candidatos separados de produção/aparelhos. Flagfalse padrão; registro preserva engines antigas; nenhuma migration nova. Suite/segurança/limites/contratos legados passaram. Plano abaixo; produção não implantada nem aceite físico declarado. |

## 4. Runtime, DEX e registro

| Artefato compilado no Linux | SHA256 |
|---|---|
| DEX28/client, idêntico à R67/R68 | `4e912015b3daac63cf48f4621ee0022448e917927a41990e9bd22e96feecb810` |
| DEX35/online novo | `f9520da87cb83d265ff9560c54c3dc672b6ce953316cfde6a02b2abf8e7f9e56` |
| client.jar utilizado por ambos | `258449792bb574e4b250e20517ed834ab5f8d60197ec80da9113be9624a9cc7c` |
| rooms.jar | `ca639c07c57f471ee6a064f3bc39ebbbc4579913c3d1792b5de3c135288650e4` |
| runtime novo | `b1b9beeffd19dccaf5475dc58659763ab858563036ba13b075fe58c7adcf0edd`/10.668.472 bytes |
| DEX30 BIOS a preservar | `1dcfa1e69686a326e22c54b9adc7c6013644e69d14642664e2ab7b5e2eee4ff4` |
| Carousel R68 a preservar | `60b944cb44a61b0c45af67ec084bb37e4ac56b21e7847c8ca7baf45c5dfb4a3d` |

Recibos [Java](recovery-r67-20261007/java-build.json), [nativo](recovery-r67-20261007/native-build.json), [engines de exemplo Linux](recovery-r67-20261007/engines.json), [adições ao registro](recovery-r67-20261007/server-engine-registry-additions.json). NativeActivity e três exports JNI, AArch64/API26/LOAD16KiB conferidos. Source upstream RetroArch69a4f0ea, arquivoSHAcecf1e3f; NDKr28c28.2.13676358. Patches e fontes congelados no manifesto do app. Patch novo reaplicado reproduziu exatamente os quatro arquivos usados no build.

IDs compilados: `bsnes-mercury-performance-79d7f9de-rs2-b1b9beeffd19` e `clownmdemu-d43c2708-rs2-b1b9beeffd19`. Core/opções/controles R64 preservados. **No Windows, derivar os IDs/hashes do binário realmente compilado**; receita faz isso automaticamente e exporta o registro. Não usar ID Linux com outro ELF nem identificar novo runtime como899e. Geolith segue launchReadyfalse; locais/BIOS/cartuchos não são alterados por esse candidato.

149 classes no DEX28 e352 no DEX35, sem duplicação entre os dois módulos; RequestProof/Tunnel presentes. O gate contra todos os outros DEX e comparação completa do APK será executado no empacotamento, ainda pendente. JDK21 + D8 antigo apresentou NPE interno na primeira tentativa; build final JDK17/API34/Java8 e D8 exato terminou. Não instalar Java globalmente: neste servidor foi extraído JDK17 privado.

## 5. Provas encerradas de laboratório

[Regressões servidor e DLL](recovery-r67-20261007/server-tests.json), [interop final](recovery-r67-20261007/interop.json), [load v1](recovery-r67-20261007/legacy-relay-load.json).

- 79 verificações v2/vetores: ring/replay/dedup/buracos/divergência/ACK perdido/pausa dos dois/foreground/ambos ausentes/180s sintéticos/geração/runtime/prova/ticket/finalizador/revogação/terminal não revertido.
- 32 vetores Java binários/RSA-PSS iguais aos esperados Python/.NET; chaves somente sintéticas e fixture privado removido.
- 46 verificações de transporte: .NET Kestrel TLS real + cliente Java real + dois TCP,1.620.000bytes exatos após quedas/retornos. Pausa JNI simulada: **não é execução de core**.
- 13 assertivas nativas do controle em host e reaplicação exata do patch, ELF16KiB/export JNI. **Não Android**.
- Regressões41salas,34social,318convites,11salas solo,53HTTP. Main suite passou vetores/cruzamento entre produtos/JSON/restart/idempotência/heartbeat, proteçãoRSA/EC/nonce/target/token/cache/atestação, catálogo/NAT/conexões/limites de download.
- Relayv1 isolado:256salas/512conexõesTLS,307.200roundtrips, zero mensagens perdidas/corrompidas e zero salas após limpeza. P95local1,1969ms/P991,858ms, picoRSS860.385.280bytes, CPU280,81s durante20,14s de carga. Componente v1 final igual ao testado; alterações posteriores foram em v2/telemetria. **Não mede v2, rede pública, Cloudflare ou Android**.

Padrão v2:64salas×256KiB×duas direções=32MiB de rings; máximo agregado configurável128MiB. É teto determinístico de buffers, não RSS medido. Configuração512/1024 da produção refere-se ao relay legado. Centenas de jogadores v2 ainda exigem dimensionamento/ensaio próprio; não extrapolar esse teste v1 para prontidão v2 ou latência externa.

## 6. Matriz obrigatória — cobertura e pendências

Versões em todas as provas isoladas: servidor 32ce9d2/app funcional4d30401, DEX/runtime da seção4. Não foram utilizados compradores/licenças reais. Os eventos abaixo são causados pelo fixture; não reconstituem o primeiro evento antigo. Memória limitada por rings/slots; CPU/RSS durante espera v2 e bateria/temperatura Android **não medidos** em nenhuma linha física.

| Cenário | Evento, estado/pausa/bytes comprovados | Resultado / falta para aceite |
|---|---|---|
| Delay1/3/6/10/20 sem romper | NEED_SYNC/barreira implementados; stall simulado passou com TCP intacto | Timings de rede reais e frames dos dois cores pendentes |
| Perda só host | Fixture abortou attachment host; ambos solicitaram pausa simulada, credencial nova, mesmos TCP, bytes exatos | TLS/TCP passou; motores/imagem/som/comandos físicos pendentes |
| Perda só convidado | Mesmo procedimento no cliente, sem Leave de host | TLS/TCP passou; Android pendente |
| Perda dos dois | Ambos attachments fechados; rings/mesma geração/sala preservados; volta/barreira/bytes passaram | TLS/TCP passou; dois Android ausentes juntos pendentes |
| Avião/Wi-Fi/dados | Troca de WSS usa nova credencial, não depende de endereço anterior | Troca física/interfaces móveis pendente |
| Ausência>60/120s | Relógio sintético180s sem membresia removida; terminal permaneceu terminal | Lógica passou; espera real prolongada/consumo e retomada de core pendentes |
| HB lento/renovação concorrente | Suite/segurança e logs de duração passaram; física pode fechar ao faltar HB sem Leave | Requisições lentas reais e novo modo de prova dos aparelhos pendentes |
| Expiração/revogação | Gen/key/prova/replay testados; Revoke removeu sala autorizada; fluxo de renew existente mantido | Expiração real durante gameplay e UI após revoke pendentes |
| Background/volta | Java suspendeu/retomou no mesmo TCP; barreira impedida quando oculto | Ciclo Activity/OS/lock/JNI/render real pendente |
| Sair na espera | V1/roomLeave e descarte dos dois tunnels recuperaram slots; Android tem evento humano idempotente | Toque/diálogo/retorno/login nos dois aparelhos pendentes |
| Reinício backend/nativo | Continuidade está só em RAM; parser/nativeEOF informa irrecuperável, sem ressuscitar bytes | Persistência não implementada; comportamento físico de kill/restart a conferir |
| Repetição/ordem/ACK perdido | Comparação/dedup/janela/gap/replay/HELLO passaram; bytes novos não duplicados | Gate isolado passou; inputs/emulação físicos pendentes |
| Versões distintas | Engine/protocolo/gen/hashes recusam incompatível; v1 regressões passaram | Dois APKs reais de gerações diferentes e mensagem de UI pendentes |

Para fechar, registrar dos dois Android: hashes instalados, correlação/primeiro evento/geração/época, pausa confirmada no JNI, frames/inputs/estado, RAM/CPU/temperatura em espera, som/imagem e saída/retorno ao menu com login preservado. SNES e Mega precisam de provas próprias. Não declarar Battletoads/queda específica corrigidos antes dessas evidências.

## 7. Produção, proxy e caminho operacional

Última observação neste turno: turborama-station-api.service ativo, PID1147382/usuário exclusivo; ExecStart `/opt/turborama-station-security-20261007-da07355/TurboRamaSuiteOnlineServer.dll`. Hash de DLL do último recibo de implantação83c8d2b3…; **não rehashado aqui**: leitura negada ao usuário lz-servidor por isolamento. Prontidão local5192:512salas/1024conexões,0/0ativas e789675bytes agregados naquele instante. Não é autorização para interromper uma partida futura.

Nginxativo/PID3172; snippet **em disco** usa HTTP1.1→127.0.0.1:5192, bufferingoff, read/send120s. `nginx -T` da configuração carregada não foi refeito neste turno. Não tomar um timeout de IO como duração máxima da partida, nem `/health` público HTML200 como prova da API. O domínioapp/v1 efetivo retornou401 JSON sem sessão. Sem mudança de DNS global, Cloudflare, Nginx, firewall, SSH, serviço ou outras aplicações.

### Operador Linux

1. Receber o recibo final do **runtime/APK realmente montado** e as adições de engines geradas; revisar diff/contrato e retornar a fonte exata. Manter todos os IDs antigos. Gerar arquivo novo com `python3 tests/StationRecovery/merge_engine_registry.py --existing <registro-efetivo> --additions <arquivo-do-build> --output <registro-novo>`; recusa colisão/overwrite e não muda o serviço.
2. Preparar release imutável a partir da fonte32ce9d2; candidato compilado local em `/mnt/DADOS/station-recovery-r67-check-20261007/server-final/publish`, hash da seção1. Se recompilar, emitir hash/recibo novo. Ler via operador o hash/configuração efetivos e ledger antes da troca; backup e restauração do alvo Station/configs/registro, preservando novos dados e outros serviços. **Nenhuma migration desta recuperação**; não refazer migrations antigas por suposição.
3. Ensaiar contrato/prova/health/limites/registry em instância isolada com o mesmo usuário/namespace, sem chaves reais nos logs. Preservar usuário turborama-station-api, papelPG/visões/ACL/read-only/rotas e `RequireVerifiedApp=false` atuais. Não importar env/segredos de outros produtos.
4. Publicação coordenada pelo operador exige alvo/artefatos concretos, nenhuma partida ativa e gates de backup/rollback/saúde. A flag começa false; ligar v2 somente após runtime/motores/contrato compatíveis e alvo de qualificação definido, mantendo v1. Não usar os scripts históricos que guardam da07355 para publicar/retornar uma sucessora: eles recusam corretamente esse alvo.
5. Devolver recibo de serviço/ExecStart/DLL/registro/configuração **ativos**, dataUTC, APIautenticada/profile/catalog/capas/autorização de download/v1/v2, prova/nonce e saúde dos demais serviços. V2 readiness local deve mostrar salas/streams/windows; público não expõe esse operador.

### Implementador Android / teste físico

1. Usar branch4d30401/retorno publicado e APKbaseR68 exato. Reproduzir receitas `build_java.py`, `build_native_runtime.py`, `package_recovery.py` no PC (E:temporários/G:candidato). Assinatura original privada; sem copiar GPL/mídias/binaries privados paraGit.
2. Entregar package.json com hash integral/certificado/DEX/runtime/engineIDs, classe-gate e todas as entradas comparadas. Revalidar sucessora antes de montar; não contornar o hashbaseR68 mudando apenas constante. Se houver nova versão, conciliar os seus deltas primeiro.
3. Atualizar **os dois aparelhos**, sem jogo ativo/desinstalação/limpeza de dados; preservar UID/data/licença/Keystore/controles/saves. Registrar modo concreto de prova/atestação pelo servidor; HTTP200 não prova hardware.
4. Executar matriz, enviar evidência sanitizada de pause/resume/inputs e logs correlacionados. Não enviar tickets/códigos/segredos/capturas pessoais paraGit. Só então avaliar ativação geral/capacidade v2.

### Retorno

Preservar release da07355/configuração/registro/isolamento como retorno do servidor. Se interromper candidato, registrar motivo e ausência de partida, desativar admissões v2/explicar sessões de memória perdidas; não fingir recuperação após trocar processo. APK novo exige capacidade/IDs compatíveis, portanto retorno ao backend anterior pode impedir **online v2**, mantendo jogos locais e clientesv1. Nunca resetar identidade/licença ou remover proof_required para mascarar downgrade; APK R68 já contém prova e precisa do certificado/dados originais. Não restaurar backupPG sobre novas vendas.

## 8. Neo Geo e outras frentes preservadas

NG-01–07 continuam pendentes: export autenticado do índice efetivo/pacotes, CRC/hashes de bytes originais versus servidos/instalados, drivers/BIOS/50CHDs e execução real. O inventário de189ZIPs do retorno b37c873 examinou tabelas centrais; não comprova esses bytes. Não renomear13nomes por tentativa ou atribuir KOF98 à BIOS sem comparação. R66 já prepara BIOS CD existente no APK: esta entrega conserva DEX30, não pede outra BIOS nem corrige CD pela rede.

Nenhuma alteração em ROM/capa/download/biblioteca/BIOS/save, licença/venda/PIX/Suite/ES/site/WhatsApp/banco/proxy por esta entrega. Não houve mensagens externas, cadastro real, migration ou reinício de produção. Testes de segurança anteriores permanecem limites reais: não afirmar acesso exclusivo ao APK, atestação completa ou segurança absoluta.

**Próxima entrega concreta:** APK candidato conciliado comR68 + registro de engines do binário real; publicação qualificada do operador e recibos de duas partidas Android. O servidor e Android agora têm implementação candidata, em vez de somente análise; o aceite físico e a implantação seguem abertos e identificados.
