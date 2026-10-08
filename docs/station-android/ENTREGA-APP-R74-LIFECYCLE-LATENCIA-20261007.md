# R74 — APP → SERVIDOR: correções reproduzidas e investigação da latência

Fonte exata do app: `557014b4ff5ec5c3c0162847d922c0587f68b0e9`, branch `fix/station-r74-session-lifecycle-20261007`.
[Fontes, receitas, provas e estado](https://github.com/luziellacerda/TurboElden/tree/557014b4ff5ec5c3c0162847d922c0587f68b0e9/versions/station-session-lifecycle-r74-20261007). Base do servidor examinada: `815ceaca49baa0feb1e2d61726c0346ddad2bd37`.

Esta é a entrega do aplicativo ao operador. Não é um retorno do servidor nem implantação Linux.
APK candidato `e56896f28b16645653bfd28311cd0a8a9a5458e6d443849916a4dede6dc7fafe`; runtime `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`.
Ambos aparelhos permanecem R73 nesta entrega. Cadastro R73 já estava ativo; não repetir aquela pendência.
Para testar R74, adicionar os DOIS novos registros exatos mantendo os OITO existentes e coordenar a ativação sem descartar partidas.

Além do cadastro, responder às oito questões da seção8: causa da primeira queda às23:52:14UTC, origem dos epochs seguintes, NeedSync13 e tempo efetivo DATA/PONG/filas/locks/SendAsync/proxy/rede. O RTT medido no app não permite atribuir sozinho a demora ao serviço.

Os recibos abaixo foram copiados do commit acima. `evidence/` corresponde a `entrega-app-r74-20261007/evidence/`; demais fontes estão no link do app. Não restaurar Activities antigas. Não declarar gameplay ou retomada validados pelos testes isolados.

---

# APP → SERVIDOR — candidato R74: ciclo de vida, entrada Android e recuperação online

## 1. Destinatário, escopo e estado desta entrega

**Destinatário:** responsável pelo **Servidor-pix / Station Android**. Este documento é um pedido técnico do aplicativo; **não é um retorno do servidor nem confirmação de implantação**.

**Produto:** TurboStations Android, pacote `org.turboramastation.frontend`; classes Java `org.emulationstation.frontend`. Tratar exclusivamente a integração Station. Não alterar outro produto Turborama, catálogo, pagamentos, licenças ou motores offline por consequência deste pedido.

**Branch do candidato:** `fix/station-r74-session-lifecycle-20261007`.

**Estado ao redigir:** APK candidato compilado, assinado e conferido entrada por entrada. Fontes e recibos finais neste snapshot; publicação identificará o commit completo no cabeçalho da entrega ao servidor. **R74 não está declarada estável nem validada em partida real de dois aparelhos.** As capturas físicas descritas neste documento são da **R73**, não da R74.

Este trabalho não realizou implantação Linux, reinício do serviço, alteração de regras de licença nem encerramento de partidas no servidor. **Não reiniciar o serviço com partidas/sessões recuperáveis retidas para simplesmente carregar o registro:** os fluxos v2 ficam em RAM e um reinício os perde. Qualquer aplicação pelo operador deve ter janela explicitamente coordenada e recibo do estado efetivo.

### Identificação final do candidato

| Campo | Valor |
|---|---|
| Commit completo das fontes R74 | **O cabeçalho da entrega no Servidor-pix identifica o commit exato que contém este snapshot; não usar o HEAD de outro ramo** |
| Caminho do snapshot R74 no Git | `versions/station-session-lifecycle-r74-20261007` |
| SHA-256 do manifesto de fontes/deltas | **84ee2979db51f12b9bbfb398df45246e16dc466d7a9aae3c9ff695f4dc4075eb** |
| SHA-256 final de `libstation_retroarch.so` | `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516` — 10669576 bytes, conferido no arquivo compilado |
| SHA-256 de `classes35.dex` | `e8c57484aa2e566edf6226a02e1f5f8ba7f9c25c7f6b050b890a092637370d3c` — 449604 bytes, conferido em `compiled-final` |
| SHA-256 do APK final | **e56896f28b16645653bfd28311cd0a8a9a5458e6d443849916a4dede6dc7fafe** |
| Tamanho do APK final em bytes | **2122907720** |
| SHA-256 do certificado conferido | **7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825** |
| Recibos da compilação composta | `evidence/java-dex-build.json`: `f1cb3ebcdee7fb15c2eb85ab5ab6b95a77f17d5c31772c5a9a5dc3720d42b900`; `evidence/local-tests.json`: `8e528be7632900402e9451d319e14672fa28776a77215963a751aa9ae5925d2e` |
| Instalação física / SHA no Samsung | **R74 NÃO INSTALADA; última versão conferida R73 / b23ff3d1** |
| Instalação física / SHA no Motorola | **R74 NÃO INSTALADA; última versão conferida R73 / b23ff3d1** |

As identidades acima são do APK final. O operador pode preparar o cadastro aditivo, mas a ativação e a instalação ainda precisam ser confirmadas separadamente. Não usar somente nome do APK ou data para identificar a entrega.

## 2. Bases exatas que foram cruzadas

### Aplicativo R73 efetivamente analisado

- Fonte publicada: `5657dce678609f25501321e307839a6e0c018d4e` no TurboElden.
- APK R73: SHA-256 `b23ff3d1e319ee050e6eb867e2643a5f66661da481dd2e3a4e50089d1604f077`, tamanho `2122890150` bytes.
- Runtime R73: SHA-256 `9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2`.
- Fonte Java local imutável: `E:\ESTUDO APK\work\station-recovery-handshake-r73-20261007-build\java`.
- Fonte nativa local imutável: `E:\R73fixed\RetroArch-69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`.
- Candidato de composição R74: `E:\ESTUDO APK\work\station-session-lifecycle-r74-20261007`.

### Servidor publicado e estado efetivo relatado pelo operador

- Retorno lido: **`815ceaca49baa0feb1e2d61726c0346ddad2bd37`**, branch `fix/station-r73-engine-registry-20261007`.
- [Documento exato do retorno](https://github.com/luziellacerda/Servidor-pix/blob/815ceaca49baa0feb1e2d61726c0346ddad2bd37/docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md).
- Fonte da DLL efetiva relatada: `ab192bf1585e30f303d041f13b36a1f9c96d2caa`; DLL SHA-256 `815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`.
- Registro efetivo R73: oito entradas, SHA-256 `266de76251a036d77db162b7cdeefaa5d7ad3093efed5455c69f3d8257ac9ed2`.
- O estado acima vem do recibo do servidor. Não equivale a uma nova inspeção Linux feita pelo app.

A última atualização de **todos os ramos remotos**, com refspec explícito no clone existente, ainda encontrou `815ceaca49baa0feb1e2d61726c0346ddad2bd37` como retorno relevante mais recente. Não havia outro retorno publicado para esta ocorrência durante esta revisão. O horário preciso dessa consulta fica em `online-research.json`; nenhum checkout foi trocado.

Foi comparado o conteúdo Git do retorno com a fonte da DLL indicada: `StationRecoveryRelay.cs` tem o mesmo blob `fe8006e30932b0fa4c4eba5d907b46ae1e2873dd` nos dois commits; `StationOnline.cs` tem o mesmo blob `5a3add94a5c023eac362188f13da6a8b63cc84e6`. Assim, a análise desses dois arquivos corresponde à base declarada em produção.

## 3. Não misturar três ocorrências diferentes

### A — primeira queda: autoridade HTTP congelada pelo Android

Esta ocorrência anterior foi cruzada com o retorno do servidor, registros Android e registros do app:

| Evento em 07/10/2026 | UTC |
|---|---|
| Último heartbeat do convidado recebido pelo servidor | `23:22:02.334574` |
| Conclusão desse heartbeat no registro do app | `23:22:02.425` |
| Android registra `am_freeze` do processo principal do convidado | `23:22:14.382` |
| Servidor encerra o transporte por `AUTH_HEARTBEAT_MISSING` | `23:23:10.719` |
| Cliente observa fechamento WSS `1006` | `23:23:10.790` |
| Processo principal do convidado volta a ser descongelado | `23:32:12.698` |

O processo separado `:station_netplay` ainda trocava PONG enquanto a autoridade HTTP no processo principal estava congelada. **PONG não substitui heartbeat autenticado nem renovação da sessão.** O congelamento ocorreu depois do último heartbeat e antes do próximo previsto. Não atribuir esta queda à ausência total de requisições do convidado: ele havia enviado tickets, dados e heartbeats com sucesso.

### B — reabertura posterior sem nova inicialização nativa

Houve tentativas seguintes em que o ticket e a abertura da Activity foram registrados, mas não apareceram novos registros JNI/WSS no convidado que reutilizava processo nativo. A localização exata do bloqueio físico dessa tentativa não foi demonstrada apenas por esses registros.

A revisão do código encontrou, separadamente, `android_app_free` aguardando o término da thread enquanto segurava o mutex necessário à finalização dessa mesma thread. Testes locais reproduziram a inversão de locks e o estado antigo de recuperação persistente. Isso fundamenta a correção de ciclo de vida, mas **não autoriza afirmar que toda tela preta observada teve exclusivamente essa causa**.

### C — partida recente: queda, ANR e sequência de epochs

**Janela obrigatória para análise do servidor:** `2026-10-07 23:49:45 UTC` até `23:53:15 UTC`, com extensão até `23:53:18 UTC` para incluir a conclusão dos últimos eventos.

- Jogo: **Battletoads / SNES**.
- **Samsung = anfitrião; Motorola = convidado.**
- Ambos com R73; geração lógica da sala **3**. A geração da sala não é o mesmo campo que o **epoch do stream TSR2**.
- Correlation/attachment/room IDs completos permanecem nos registros privados. O servidor deve correlacioná-los internamente por janela, papel, geração e jogo; não publicá-los junto de credenciais ou dados pessoais.

| Evento observado no app | UTC |
|---|---|
| Samsung recebe epoch1 / Playing2 | `23:49:52.690` |
| Motorola recebe epoch1 / Playing2 | `23:49:52.948` |
| Últimos heartbeats antes da queda: Motorola / Samsung | `23:52:10.443` / `23:52:12.960` |
| Samsung recebe epoch2 / Waiting0 | `23:52:13.842` |
| Motorola recebe epoch2 / Waiting0 | `23:52:14.014` |
| Motorola recebe socket-close `1006`, depois entra em espera de transporte | `23:52:14.188` / `.189` |
| ANR Motorola: MotionEvent aguardando cerca de 5 s | `23:52:19` |
| ANR Samsung: MotionEvent aguardando cerca de 10 s | `23:52:23` |
| Samsung recebe epoch3 / Waiting0 | `23:52:49.216` |
| Samsung / Motorola recebem epoch3 / Playing2 | `23:52:52.857` / `23:52:53.071` |
| Samsung / Motorola recebem epoch19 / Waiting0 | `23:53:14.503` / `23:53:14.884` |
| Samsung / Motorola recebem epoch19 / Playing2 | `23:53:15.678` / `23:53:15.763` |

Os registros continuam além dessa janela: Samsung chega a epoch20/21. Os heartbeats também continuam — por exemplo Samsung `23:52:33.852`, `23:52:55.320`; Motorola `23:52:30.653`, `23:52:50.852`, `23:53:11.086`. Portanto **não reutilizar automaticamente o diagnóstico de falta de heartbeat da ocorrência A para a sequência C**.

Os horários Android foram convertidos de epoch para UTC; não são medição de atraso de uma via entre relógios sincronizados.

Os stacks de ANR coletados apontam a thread nativa no descanso do ramo de espera de recuperação (`runloop.c`, referência R73 em torno da linha7370; endereço simbolizado no runtime R73). Esse ramo retornava antes do caminho habitual que consome/confirma os eventos da fila de entrada Android. Isso explica tecnicamente a capacidade de ficar esperando rede e, ao mesmo tempo, não confirmar MotionEvents. Não descrever como simples CPU saturada ou como um stack Java comprovadamente bloqueado.

## 4. O que muda no candidato R74

### Autoridade HTTP e Binder

1. `StationRoomsActivity` cria um owner de sessão e registra nonce privado + roomId + generation, permitindo um único lançamento pendente/ativo. A abertura usa `startActivityForResult` para também observar encerramento anterior ao ATTACH.
2. `StationSessionService` é um serviço **privado, no processo principal**, sem `startService`, `START_STICKY` ou novo foreground service.
3. A Activity nativa em `:station_netplay` vincula-se explicitamente com `BIND_AUTO_CREATE | BIND_IMPORTANT`. Messenger realiza IPC entre processos; UID e identidade da sessão são validados. Não há cast de Binder local entre processos.
4. O transporte começa depois do ACK do vínculo. O vínculo permanece em `onStop`; `onDestroy` o libera. Enquanto o cliente nativo está em primeiro plano, a dependência informa ao Android a importância do processo dono da autoridade.
5. Morte/desvinculação faz limpeza local. Não fabrica `leave` nem `recovery-failed`. Os eventos humanos/terminais explícitos mantêm idempotência e aceitam as ordens cruzadas de entrega entre Binder e ResultReceiver.
6. Pedidos simultâneos de `resume-relay` são coalescidos enquanto um está pendente. Não são criadas duas autoridades de sessão nem aumentada a concorrência HTTP.
7. `StationGameSession.LaunchReturn` guarda requestCode crescente + nonce + room + generation; a identidade é salva/restaurada em Bundle. O retorno exato da Activity limpa apenas o owner correspondente, inclusive se o processo nativo falhar antes do ATTACH. Callback antigo não limpa uma sessão nova. O código de resultado não é convertido em `leave` ou `recovery-failed`, e o lançamento pendente impede abrir outro antes do retorno. Não foi adicionado timer para resolver essa condição.

A documentação Android sustenta o vínculo e a propagação de importância. Isso **não promete execução indefinida quando todo o aplicativo está em segundo plano, quando o sistema mata processos ou quando a instalação muda**. Ver [serviços vinculados](https://developer.android.com/develop/background-work/services/bound-services) e [BIND_IMPORTANT](https://developer.android.com/reference/android/content/Context#BIND_IMPORTANT).

A API oficial de [resultado de Activity](https://developer.android.com/reference/android/app/Activity#onActivityResult(int,%20int,%20android.content.Intent)) documenta retorno cancelado inclusive após crash. O fluxo conserva Activity padrão, sem `NEW_TASK` nem `noHistory`; os testes JVM modelam o callback, e a entrega real desse resultado pelo Android ainda pertence à aceitação física.

### Finalização e nova abertura do motor

- Saída humana solicita quit à thread nativa por flag atômica e acorda o Looper; não há timeout inventado que mate a partida.
- A espera/join não mantém o mutex de finalização bloqueado. Callbacks deixam de esperar eternamente por estado que a thread já encerrada não pode entregar.
- Uma reserva nativa impede segunda Activity/core concorrente até a conclusão da anterior. O reset do estado de recuperação ocorre antes da nova thread, nunca durante reconexão da mesma partida.
- O encerramento normal upstream pode terminar o processo privado. A afirmação correta é preservar a partida durante recuperação, não prometer que o processo nativo nunca termina.

### Entrada durante espera e pacing normal

- Durante espera de recuperação, o candidato drena/confirma entrada e eventos Android antes da checagem de estado. Não executa `retro_run`, rollback ou geração de frames de jogo nesse ramo.
- Remove exclusivamente a escalada de `NETPLAY_STALL_RUNNING_FAST` para a flag persistente que mandava `NeedSync13`. Mantém o próprio stall/catch-up upstream, processamento de entrada, recuperação de WSS realmente perdido, protocolo e barreira de READY.
- O problema de código foi reproduzido: o upstream pode encerrar um stall transitório, mas a flag Station antiga continuava marcada até o próximo tick Java, gerando uma ressincronização externa desnecessária.
- A função de flush/handshake corrigida na R73 permanece idêntica: SHA-256 do texto da função `d3a42419124dda592fea217088ed4e4687bf63718f657299ac8d73ad9fa41e12`.

O comportamento de catch-up está documentado pelo [Libretro](https://docs.libretro.com/development/retroarch/netplay/) e aparece tanto no [código oficial v1.22.2](https://raw.githubusercontent.com/libretro/RetroArch/v1.22.2/network/netplay/netplay_frontend.c) quanto no [upstream atual consultado](https://raw.githubusercontent.com/libretro/RetroArch/master/network/netplay/netplay_frontend.c). Não se alteraram presets de latência, sincronismo, autorização ou compatibilidade como substituto de diagnóstico.

## 5. Contrato preservado

Permanecem sessão autenticada, prova por requisição, identidade do aparelho, assinatura/validação de catálogo e o canal Station. Não adicionar hosts legados/CDNs dentro do APK por este pedido.

No online permanecem `station-stream.v2`, identidade completa do motor/core/runtime/conteúdo/opções/geração, tickets de uso controlado, `host-listening`, `resume-relay`, offsets de bytes accepted/delivered, pausa nativa e barreira READY. `NeedSync13` continua sendo um tipo válido no servidor; a revisão corrige a origem indevida desse pedido no cliente. Não retirar verificação de licença, heartbeat ou proof e não usar PONG para simular autenticação.

Este documento não pede alterar o timeout de autoridade ou tornar sessões autorizadas eternas. Perda real de rede deve permanecer em espera e usar o contrato de retomada; revogação, processo nativo perdido e incompatibilidade exigem resposta honesta, não recriação silenciosa de uma partida como se fosse a mesma.

## 6. Registro solicitado: duas entradas novas, oito existentes preservadas

O ELF online é compartilhado por SNES e Mega. A compilação confirmou a mudança de bytes: **o mesmo novo runtimeSha256 aparece em duas entradas de engine**, com IDs novos correspondentes. Não inventar dois ELFs diferentes nem reutilizar os IDs/hashes R73 para bytes R74.

| Campo | SNES | Mega Drive |
|---|---|---|
| `engineId` final | `bsnes-mercury-performance-79d7f9de-rs4-804b2acfea4c` | `clownmdemu-d43c2708-rs4-804b2acfea4c` |
| `runtimeSha256` final | `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516` | `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516` |
| `coreSha256` esperado se preservado | `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b` | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |
| `recoveryProtocol` | `station-stream.v2` | `station-stream.v2` |

O JSON de adições já preparado em `compiled-final/evidence/server-engine-registry-additions.json` foi cruzado com a tabela: contém somente essas duas entradas. **No JSON do registro do servidor a chave é `id`; no manifesto do aplicativo é `engineId`.** Usar o JSON de adições publicado junto das fontes, preservando seus campos `id`, `platform`, `coreSha256`, `runtimeSha256` e `recoveryProtocol`; não converter a tabela em um esquema inventado.

O integrador deve conferir os cores acima contra o APK final antes de entregar o JSON real. Não são valores para corrigir silenciosamente se o build divergir.

**Pedido ao operador após receber identidades finais:** preparar inclusão aditiva dessas duas entradas, preservando integralmente as oito ativas. Devolver diff/recibo do conjunto anterior e posterior, hashes do registro e confirmação do snapshot autenticado que o telefone receberá. A expectativa é dez entradas se ninguém tiver acrescentado outras entretanto. Se houver alteração concorrente, preservar todas e relatar a diferença; não impor cegamente um total.

Confirmar também qual processo/DLL/registro está efetivamente servindo. Arquivo modificado no Git/disco não prova que um registro carregado apenas na inicialização está ativo. Coordenar qualquer recarga necessária sem descartar partidas retidas.

## 7. Lentidão: o que foi conferido e o que falta medir

### Conferido no código

- App R73 já chama `setTcpNoDelay(true)` no WSS e no TCP local. A implementação de WebSocket aplica a opção no socket TLS real antes da conexão; o writer escreve e faz flush por frame. Não faltava apenas esse setter.
- No servidor, `Changed()` acorda o envio por `TaskCompletionSource`; `Send()` chama `Next()` e `SendAsync` imediatamente quando há frame. Não foi encontrado sleep de100/250ms no fluxo DATA/PONG.
- `SendAsync` não ocorre segurando o lock de janela. O lock protege estado e cópias limitadas a16KiB. A publicação de estado toma o lock do hub e altera memória, sem SQL nesse trecho.
- As esperas de10s e15s são do watchdog e prune, não do envio de cada pacote.
- A configuração nginx versionada tem `proxy_buffering off` e `proxy_cache off`. A [documentação nginx](https://nginx.org/en/docs/http/ngx_http_core_module.html#tcp_nodelay) informa TCP_NODELAY por padrão para SSL/WebSockets, mas isso não substitui conferir a configuração efetiva.

### Métricas não equivalentes

O relatório do servidor às `23:22:23.991727 UTC` informou34 comandos, p50 `0,08795ms`, p95 **`0,1756ms`**, máximo `0,7365ms`. Esse cronômetro mede **Command interno**. Não inclui necessariamente toda a autenticação, proxy, fila WSS, rede, execução Android ou ida e volta.

Amostras posteriores do aplicativo têm RTT WSS diferente entre aparelhos (resumo do coletor: Samsung mediana aproximadamente122ms; Motorola aproximadamente359ms). Não subtrair diretamente o p95 interno do servidor desses RTTs nem declarar, por esse cálculo, que a rede ou o serviço é culpado. São caminhos, períodos e instrumentos diferentes.

Há evidência de CPU/temperatura sem saturação generalizada no instante amostrado; isso não exclui pausas pontuais de thread, GC, fila, retransmissão ou custo de rollback. Não há neste handoff medição real de FPS do core que autorize mudar presets.

## 8. Perguntas objetivas que o retorno do servidor deve responder

Responder por item com **evidência, período, origem do dado e limite**, usando a geração3 e os papéis indicados na seção3C:

1. **Queda de `23:52:14`:** qual foi o primeiro evento do servidor que encerrou/retirou a conexão do convidado? Informar causa registrada, papel, epoch, idades dos heartbeats e offsets. `1006` observado no app é sintoma; não é sozinho a causa no servidor.
2. **Epochs2–19:** para cada incremento, classificar `Detach`, `Suspend11`, `NeedSync13`, ação humana, revogação ou outro caminho de código. Informar recebimento e emissão em UTC, papel de origem e estado anterior/posterior. Não deduzir NeedSync apenas porque o epoch aumentou.
3. **Tempestade após retomada:** houve `NeedSync13` repetido recebido de Samsung, Motorola ou ambos entre `23:52:52` e `23:53:15`? Quantos? Os sockets permaneciam anexados? Os heartbeats/proofs continuavam válidos? Distinguir esta sequência da primeira queda por freezer.
4. **Custo DATA/PONG:** medir com relógio monotônico, por conexão/direção, os tempos de recebimento completo → aquisição do lock → seleção para envio → início e fim de `SendAsync`. Entregar contagem, p50, p95, p99 e máximo por tipo/tamanho; medir PONG separadamente de DATA.
5. **Filas:** registrar comprimento/idade da fila de envio e bytes pendentes nas janelas por direção durante a mesma amostra. Janela de256KiB é limite de retenção, não afirmação de que sempre se espera acumular esse tamanho.
6. **Serviço e SO:** correlacionar starvation/pendências do ThreadPool, pausas GC, pressão de memória, tempo de espera de locks, TCP RTT/retransmissões/send queue e descartes de interface. Se a ferramenta não estiver disponível, informar a lacuna; não fabricar histogramas.
7. **Proxy efetivo:** informar caminho ativo, HTTP Upgrade e buffering/TCP_NODELAY efetivos, upstream e existência de outro proxy/CDN no caminho. Preservar autenticação e pin TLS. Não mudar esses itens sem mostrar a divergência encontrada.
8. **Registros R74:** depois do build final, devolver inclusão exata das duas entradas e confirmação do registro efetivo/snapshot autenticado, sem remover motores anteriores.

Usar agregados com baixa interferência. Não habilitar logs pesados de cada frame/payload em produção para depois atribuir ao serviço a lentidão criada pela própria coleta. IDs completos necessários à correlação ficam privados; publicar apenas resumo saneado, sem tickets, provas, licenças, ROMs, saves ou logs pessoais.

## 9. Testes existentes e limites

Os recibos do snapshot final prevalecem sobre uma contagem copiada antes do término da composição.

- Java: classes reais `Service`, `Link`, `GameSession`, `Registry`, com fixtures de Android/Binder/HTTP. Recibo lido e conferido: **101 checks** (22 registry +79 lifecycle), **42 guardas**, 201 fontes de produção. SHA-256 de `evidence/session-tests.json`: `ca5ee0fcfe2eae2c9379ab17aa632f03212f4f9a0bea1dd6fde698103a365942`. Abrange UID/identidade, ACK tardio, morte Binder, false/throw/null/died/disconnected, ordens DETACH/eventos4/6, saída idempotente, coalescing de grants, lançamento único, crash anterior ao ATTACH, retorno antigo, recriação de configuração e retorno depois dos eventos4/6. Os testes foram repetidos sobre as seis fontes finais do snapshot. A suíte histórica foi executada separadamente: 1.206 verificações com as 201 fontes compostas, registradas em `evidence/local-tests.json`.
- Native lifecycle: funções reais extraídas, threads/locks/condições do host e Looper/limpeza modelados. Reproduz falhas R73 e passa encerramento, reabertura, callback tardio, identidade JNI, reserva de Activity e reset. Não é um Android real.
- Entrada: simulação de12s de espera; baseline deixa2400 eventos pendentes, candidato confirma2400 sem avançar frames de jogo. Não é um teste físico de ANR.
- Pacing: trechos C reais e métodos Java `tick/lost` extraídos. Em200 catch-ups, R73 mantém200 flags antigas; candidato zero. Tick antigo envia SYNC13, candidato não; perda WSS real ainda pausa e preserva offsets. Não houve servidor, rede real ou emulador executando nesse probe.

Revisão independente da diff e da receita de pacing confirmou a remoção exclusiva de três linhas de escalada, preservando `netplay_sync_input_post_frame` e a condição upstream de stall. Fonte final `netplay_frontend.c`: `b1d0f5ddea0c1fb2d675fd08b73503fd80b03c71a6a60c8dea7084a96950d4fe`; receita `pacing_probe.py`: `ac5f477725cccbb09ab8765577278e740349ac168f15c45d96b1cd04b1601adb`. A revisão não fez uma nova execução física dos testes.

O teste isolado demonstra a diferença de código. A correlação de NeedSync13 com a tempestade física ainda precisa da resposta da seção8; a correção de todos os sintomas **não está declarada comprovada**.

## 10. Aceitação física planejada após build, registro e instalação conferidos

1. Verificar o mesmo APK/certificado/runtime/core nos dois telefones e o engine correspondente no snapshot autenticado. Preservar dados, jogos, saves e licença; não desinstalar como atalho.
2. Criar sala nova, jogar Battletoads com Samsung anfitrião e depois inverter os papéis. Mostrar nomes/controles e manter resposta aos toques e Voltar.
3. Manter partida ativa por tempo suficiente para observar **pelo menos duas renovações completas de sessão em cada aparelho**, registrando heartbeats e vínculo do serviço. Duração isolada não substitui evidência de renovação.
4. Durante espera/sincronização, tocar, usar Voltar/menu, verificar confirmação dos eventos Android e ausência de ANR. Core não deve avançar frames enquanto a barreira exige pausa.
5. Interromper/restaurar a rede de um aparelho em ensaio coordenado, sem fechar o aplicativo. Conferir pausa, retomada autenticada, accepted/delivered monotônicos, READY e continuação real da mesma partida. Não chamar nova partida de recuperação.
6. Executar saída humana pelo menu; conferir retorno às salas/plataformas, uma saída remota e ausência de heartbeat/trabalhadores da partida encerrada. A presença do catálogo é outra função e não deve ser confundida com esse owner.
7. Abrir nova partida e repetir para detectar estado antigo, processo reaproveitado, duas Activities ou reset indevido. Se o Android terminar todo o processo, registrar o limite sem alegar recuperação do estado que já foi perdido.
8. Comparar epochs, causa do primeiro encerramento, RTT e tempos do servidor durante jogo parado e com entradas reais. Não aprovar estabilidade apenas por ter chegado a Playing2.

## 11. Formato esperado do retorno

Publicar um **novo retorno SERVIDOR → APP** identificando este pedido R74, com branch, commit completo, data UTC, fonte e DLL/registro ativos. Informar o que foi apenas revisado, o que foi testado isoladamente e o que foi aplicado pelo operador. Responder os oito itens da seção8 e anexar JSONs saneados/manifestos reproduzíveis.

Nome sugerido: `docs/station-android/RETORNO-SERVIDOR-APP-R74-LIFECYCLE-LATENCIA-20261007.md`. Se usar outro nome, apontar explicitamente o vínculo com este documento; não responder apenas com um handoff anterior ou com a cópia deste pedido.

## 12. Fontes locais da revisão, ainda não publicadas por este revisor

- `work/r74-transport-review/review-plan.json`: revisão independente, casos críticos e hashes das fontes Java lidas.
- `work/r74-transport-review/online-research.json`: pesquisa oficial, identidade dos blobs do servidor e limites das medições.
- `E:\ESTUDO APK\work\station-session-lifecycle-r74-20261007\java-delta`: fontes Java candidatas e recibos dos testes.
- `work/r74-native-delta`: fontes nativas candidatas e probes `lifecycle`, `recovery_input` e `pacing`.
- Capturas físicas privadas permanecem no PC sob `E:\ESTUDO APK\work\station-session-lifecycle-r74-20261007\evidence`. Publicar somente resumos saneados, nunca essa pasta inteira.

**Fechamento desta versão documental:** APK, runtime, DEX, certificado, fontes e engine IDs finais conferidos. Exatamente quatro entradas substituídas; 13.221 preservadas, incluindo toda a mídia e todos os cores. Registro efetivo R74 e instalação aguardam conclusão. Nenhum Linux reiniciado; nenhuma prova de estabilidade R74 em dois aparelhos alegada.
