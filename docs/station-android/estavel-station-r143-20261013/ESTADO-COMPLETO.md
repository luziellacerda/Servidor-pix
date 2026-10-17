# TurboStations R143 estado estável e histórico de manutenção

## Referência aprovada em 13 de outubro de 2026

O proprietário aprovou a R143 como versão estável e ponto de recuperação após jogar Top Gear 3000 em Corrida VS com três participantes e informar que Bomberman também funcionou. A referência é a tag e o ramo `estavel-station-r143-20261013`. A fonte executável continua `26257869b59af8ecea44932ab287705990a9acbe`; a promoção acrescenta documentação e referências Git, sem gerar outro APK.

O APK tem SHA256 `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, 2.715.024.399 bytes e pacote `org.turboramastation.frontend`. Assinatura original v2/v3, certificado `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` e alinhamento de 16 KiB. [Manifesto](MANIFESTO-ESTAVEL.json), [instalação](evidence/installation-moto-g52.json), [Top Gear](evidence/three-player-human-result.json) e [Bomberman](evidence/bomberman-human-result.json) fixam o resultado aceito.

O moto g52 está na R143. Samsung A56 e Edge 30 têm como última instalação comprovada a R141. A estabilidade designada pelo proprietário cobre o ponto aceito e os testes descritos; quatro ou cinco telefones humanos, todos os jogos e partidas prolongadas continuam fora desse aceite.

## Como entender o aplicativo inteiro

O [dossiê completo da base R134](https://github.com/luziellacerda/TurboElden/blob/estavel-station-r143-20261013/docs/handoff-completo-r134-20261011/DOSSIE-COMPLETO.md) e seu [índice](https://github.com/luziellacerda/TurboElden/blob/estavel-station-r143-20261013/docs/handoff-completo-r134-20261011/README.md) descrevem telas, serviços, login, licenças, armazenamento, downloads, instaladores, resolver de jogos, emuladores, multiplayer, endpoints e operação. Seus inventários incluem todas as entradas do pacote, bibliotecas nativas, classes, métodos e referências de fonte. Use a base como mapa funcional e as alterações R135 a R143 abaixo para entender esta versão. O [handoff vigente](https://github.com/luziellacerda/TurboElden/blob/estavel-station-r143-20261013/docs/server/HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md) contém os retornos do servidor e a cronologia integral.

O fluxo preservado é: entrada oficial e licença → sessão autenticada → catálogo e conteúdo autorizado → biblioteca/carrossel → baixar e instalar → resolver/owner offline ou sala online → perfil assinado por jogo e modo → motor → saída humana → retorno à biblioteca. Capas, vídeos, LEDs, pesquisas, ajustes dos motores, caches, filas, saves e limites comerciais pertencem ao fluxo original. O ponto R143 não introduz redesign ou ajuste geral de processamento.

Na biblioteca, a presença de um emulador ou o botão online não basta para aprovar todos os títulos. O servidor assina conteúdo, edição, motor, opções e modo concretos; o aplicativo conserva essa validação. PS3 e PS4 continuam offline. PS2 e outros setores pendentes não recebem novas implementações por esta promoção.

## Reparos preservados até este ponto

| Revisão | Mudança efetiva e diagnóstico | Resultado e limite |
| --- | --- | --- |
| R135 | Corrige o ciclo de monitores na saída Dolphin e a saída humana de geração antiga apenas após rejeição exata e snapshot autenticado compatível. Mudanças em DEX35 e marcadores, com preservação das demais entradas. | Instalada nos três aparelhos. Não resolveu sozinha todos os carregamentos e ANRs. |
| R136 | ANR real no g52 ligou a UI, RenderThread e compilação de shader de capa no driver Adreno. Só nesse modelo, impede o worker/TextureView do efeito online e mantém a imagem original. | Usuário abriu e navegou sem aviso. Candidata com marcadores antigos foi retirada; versão final corrigiu a cadeia. |
| R137 | Activity conclui finish depois do quit aceito. | Usuário voltou às salas, mas tombstone revelou abort nativo em ALooper_wake antes do finish. Não recuperar R137 como saída plenamente corrigida. |
| R138 | Carga SNES DSP usa HLE existente; limpeza nativa idempotente ocorre antes de liberar looper, inclusive retorno antecipado do motor. | Super Mario Kart e Top Gear carregaram e executaram 120 quadros em ensaio isolado. Isso não era prova de partida sincronizada. |
| R139 | Confere recursos antes de iniciar/esperar; mantém uma chave JNI por processo e detach só de threads anexadas pelo helper. | Cenários host e probe Android com JavaVM/pthread reais passaram. Core, FPS e timers preservados; ganho térmico físico não medido. |
| Diagnóstico temporário | Quatro tentativas de Top Gear em g52/Edge registraram arquivos DSP4 ausentes, mostrando que o HLE preparado não estava sendo escolhido. | APK temporário e logs privados serviram ao diagnóstico. Não usar como release/donor estável; logger removido. |
| R140 | Liga global_core_options e desliga game_specific_options na sala para usar suas opções canônicas, sem overrides offline de jogo/pasta/core. | Resolver extraído passou 20 cenários. Mesmo core/opções HLE; instalada no g52 e preservada na R141. |
| R141 | Somente o GET canônico do artefato passa ao host direto de downloads. Autorização, catálogo, sessão, capas, mídia e online mantêm origem original. | Download Android concluído: 546.776.387 bytes em 75.288 ms. Proprietário percebeu mais que o dobro de velocidade; razão pareada não foi medida. |
| R142 | Entrega dispositivos SNES por remapeamento privado .rmp da sala, pois as chaves no cfg global não eram lidas naquele caminho do runtime. | Teste de três jogadores falhou: auto_remaps false apareceu antes de uma segunda chave true; parser usa primeira ocorrência. Não é fallback estável. |
| R143 | Substitui a linha false por true uma vez, exclusivamente no helper SNES e perfil multitap assinado. Ausência da política recusa preparação. | Top Gear VS com três participantes e Bomberman funcionaram nos relatos do proprietário. Mesmo APK assinado instalado no g52. |

As fontes e os recibos de cada revisão ficam em `versions/`, `integration/` e `docs/server/r139` a `r143`. A [auditoria de desempenho](https://github.com/luziellacerda/TurboElden/blob/estavel-station-r143-20261013/audits/station-performance-preservation-20261013/README.md) compara o backup otimizado, a base anterior e os reparos; não substituir essa auditoria por afirmação de ganho de CPU/temperatura na R143.

## Causa do multitap e correção exata

O core declara `library_name=bsnes-mercury`. Para SNES com `snes-multitap-port2-v1`, a sala cria `input-devices/bsnes-mercury/bsnes-mercury.rmp`; não copia remapeamentos offline compartilhados. O vetor aprovado é `[1,257,1,1,1]`: porta 1 Joypad, porta 2 Multitap e demais dispositivos conforme o perfil existente. Escrita atômica e caminho privado pertencem à R142, preservados na R143.

No runtime correspondente, as chaves de dispositivo são consumidas pelo leitor de remapeamento. O parser real mantém a primeira ocorrência de uma chave. A R142 acrescentava true depois do false original, deixando a leitura automática desativada. A R143 substitui o false existente e evita duplicação; não troca motor, ordem de slots ou mecanismos READY/ACK.

Os testes executaram seis corpos reais do parser e headers conferidos, com três cenários: false seguido de true continua false; true único habilita; false único permanece false. O ensaio anterior de leitor/controlador confirmou o dispositivo 257 na porta 2 sob fixtures controlados. [Qualificação](evidence/host-parser-and-controller.json). A configuração privada do Android não foi lida por esses fixtures.

A [documentação Libretro do core](https://docs.libretro.com/library/bsnes_mercury_performance/) descreve Multitap na porta 2; o [issue RetroArch 10424](https://github.com/libretro/RetroArch/issues/10424) registra dificuldades semelhantes com netplay. Esses materiais orientaram a investigação; a duplicação foi confirmada na própria cadeia R142. O máximo do jogo Top Gear 3000 VS é quatro; suporte técnico a cinco portas não aprova cinco neste título.

## Integridade e desempenho preservado

Comparada à R142 exata, a R143 verifica 13.860 entradas: 13.855 preservadas, cinco substituições, zero adições/remoções. Substituições: `classes35.dex`, `assets/station-online/engines.json` e três bibliotecas de identidade PSX/Mupen/Dolphin. As bibliotecas renovam fingerprints ligados a DEX35; não alteram implementação de emulação. [Prova final](evidence/final-apk-proof.json).

Trinta e nove DEX permanecem exatos. DEX35 contém 608 classes; 607 permanecem exatas e só `StationRetroLaunch.prepareSnesMultitap` muda. Nenhuma classe ou método novo. A comparação inversa recupera a classe anterior e verifica instruções/operandos/destinos. A montagem recusada e as preparações anteriores continuam arquivadas; a normalização aceita só labels consecutivos no mesmo endereço, sem pular instruções.

| Componente preservado | SHA256 |
| --- | --- |
| DEX28 downloads R141 | `534653399c359834c04733a18220524cfd95480f23e0f07d49babcc0ef2eb5bd` |
| Runtime RetroArch R139 | `ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378` |
| Core SNES | `0a3ac7b4fa5da318b1c993d8867aa944eaa2d012680b49e9929bf7a4a398f527` |
| DEX35 final R143 | `0cc5f639202ed3da50c246a9b07eda91c8c8d81bdc9764c67f08f3da459a74c4` |

Mantêm-se filas, buffers, cache/preload, FPS/pacing, menus, controles, políticas HLE, Manifest/resources, autenticação, licenças, proof, tickets, transporte e READY/ACK conforme os recibos de preservação. Não reativar hashes de ROM Android nem introduzir presets gerais sob o nome de recuperação. A R143 não mede aquecimento, bateria ou consumo de CPU em comparação física controlada.

## Condições das partidas e testes aceitos

Top Gear 3000 Corrida VS conserva contagens assinadas `[2,3,4]` e perfil SHA256 `88568b97800afa017e3c75684effe23f3aad8e93f5b2ac947a0638a4268bd8f3`. Campeonato em dupla usa dois participantes e perfil padrão; Super Mario Kart SNES também é para dois. Nome do jogo, edição, conteúdo, engine, perfil e modo precisam coincidir com o vínculo assinado. Número na sala e PONG não comprovam controles de carros.

O usuário confirmou “jogou 3 jogadore” após a R143. Sua resposta anterior “entrou 4 jogadores” indica avanço da seleção, mas não comprova quatro telefones humanos. Bomberman também recebeu relato positivo; a sala observada antes mostrava Super Bomberman 3, porém edição, modo e contagem não foram repetidos na confirmação. Na captura posterior ao Top Gear o g52 já estava no frontend, sem processo online ativo; não há captura da corrida ou controles instrumentados.

## Situação do servidor

O retorno da R141 informa cadastro aplicado em 13/10/2026 às 20:19:44 UTC, 17:19:44 em Fortaleza. Totais: 39 engines, 11.604 perfis, 6.743 modos; catálogo 46, 4.690 IDs, 4.435 visíveis e 255 aliases. Foram preservados os prefixos anteriores de 35 engines, 11.526 perfis e 6.665 modos. A renovação cobre os mesmos 78 modos em 44 jogos: 66 N64, nove GameCube, três Wii. PSX mantém zero novas aprovações HLE por edição. [Retorno aplicado](https://github.com/luziellacerda/TurboElden/blob/estavel-station-r143-20261013/docs/server/r141/server-return.json).

O recibo informa DLL `ef5270b3d47bcc38407cf4821a9b33a13935409f54835874ab9926f31f73ca4a`, PID 2867363, sem reinício naquela entrega. É estado retornado naquela data, não inventário live produzido por esta promoção. `/mnt/DADOS/station-r141-20261013/applied.json` existe conforme retorno: não reaplicar a R141. O preparador atual da lista fixa evita duplicação de modos; preservar o operador corrigido e suas 17 verificações.

SNES mantém seu cadastro existente na R143. Os quatro IDs independentes PSX/N64/GameCube/Wii mudam com DEX35; o kit R143 está preparado e **ainda não aplicado**. Kit privado SHA256 `a4ba210580ea68beb0086c2c986c6a884e888a1e9ad599bd7e9d9ae33364f606`, 12.161.998 bytes, 38 arquivos, seis bibliotecas e vinte dependências, renovando somente os mesmos 78 modos. [Recibo](evidence/private-kit-receipt.json). A palavra estável não substitui esse cadastro nem autoriza deploy/restart da API.

## Endpoints e contratos preservados

Base de serviços: `https://app.lzgames.com.br`. A partir da R141, exclusivamente o GET canônico de artefato usa `https://downloads-station.lzgames.com.br`, com CA/hostname/pin próprios, Bearer/proof e vínculo ao grant preservados. Sem redirect ou fallback reutilizando grant.

| Rotas | Função e condição |
| --- | --- |
| POST `/v1/station/activations/challenge`, `/activations/complete` | Ativação por código/desafio, nonce e chave do dispositivo; envelopes assinados e licença própria. |
| POST `/v1/station/challenges`, `/sessions`; GET `/me` | Abrir/renovar sessão e obter perfil autenticado; os campos de contexto são ligados à licença/aparelho. |
| GET `/v1/station/catalog?metadata=1`, `/covers/{coverId}` | Catálogo/revisões e capas por IDs autenticados. O inventário base e os overlays de catálogo têm limites próprios, não inferir conteúdo ausente por nome visual. |
| POST `/v1/station/downloads/authorize`; GET `/artifacts/{grantId}` | Grant de uso único por item/revisão; somente o GET vai ao host direto. Tamanho/MIME/formato/publicação atômica e cancelamento originais. |
| POST `/v1/station/online/command`, `/online/events` | Sala/presença/social/eventos; preservar poll único e recuo de consultas, sem repetir mutações por hipótese. |
| POST `/v1/station/online/multiplayer/command` | Snapshot/capacidades/create/join/ready/start/leave; exige schemaVersion 1, multiplayerVersion 3 e capability station-multiplayer.v3. |
| WSS GET `/v1/station/online/multiplayer/relay` | Ticket único e proof; station-stream.v3/TSR3, links anfitrião-convidados, HELLO/READY/ACK/epoch/replay existentes. |
| GET `/v1/station/online/relay` | Compatibilidade legada v1/v2, distinta do relay multiplayer v3. |
| GET `/v1/station/media/catalog?requestId=...`, `/media/files/{sha256}` | Contratos do cliente para vídeos imutáveis com tamanho/hash/codec; presença do contrato não prova implantação dessas rotas. |
| `/ready/station/online` e `/diagnostics` correspondente | Diagnóstico administrativo local; não concede entrada em jogo. |

Os caminhos abreviados compartilham o prefixo da linha. A especificação de campos, limites, assinatura e efeitos permanece no dossiê-base, capítulos de serviços e endpoints, e nos contratos/retornos posteriores. Não copiar tokens, activationCode, chaves, senhas ou URLs privadas ao Git. Este checkpoint não modifica contratos comerciais ou políticas de autorização.

## Pendências a consultar antes de novos reparos

- Testar quatro telefones humanos no Top Gear VS e cinco em jogo/mode compatível; esclarecer edição/modo/contagem do Bomberman quando necessário.
- Instrumentar controles, sincronismo e áudio, partida prolongada, saída após perda real de sinal e diferentes aparelhos.
- Avisos JNI StackOverflowError na impressão de exceções da R142 continuam sem stack causal isolado; a correção da flag não demonstra solução desse aviso.
- Crash gráfico anterior de mutex destruído e respostas relay409 sem código causal seguro continuam incidentes separados.
- Aplicação do kit R143 dos quatro owners independentes está pendente. Outras plataformas/PS1 HLE por edição e overlay NES têm seus próprios setores e não recebem liberação global.
- Cancelamento e grant novo em download Android, comparação pareada de velocidade e medições físicas de CPU/temperatura permanecem sem ensaio correspondente.

## Backup e precedência dos registros

Backup privado existente: `F:/TurboStations-Backup-Completo-20261011`. O ponto completo fica em `desenvolvimento/reconstrucao-atual/estavel-station-r143-20261013`: APK R143, donor R142, ferramentas, históricos Git completos, fontes/receitas, autoridade de assinatura privada, arquivos de compilação/prova, instalador, kit servidor e registros. [Restauração](RESTAURACAO.md) explica as etapas; `INPUT-MAP-R143.json` e `backup-receipt.json` privados conferem os arquivos reais e as referências. As versões e checkpoints anteriores permanecem preservados.

O mapa integral do APK final está no recibo privado `assembly/unsigned-package.json`. O dossiê R134 é o mapa histórico, não uma alegação de que todas as suas entradas ainda sejam R143. Fontes/métodos dos overlays e o delta final documentam as mudanças.

O manifesto desta tag registra a aprovação atual. Provas de assinatura/compilação mantêm `candidateOnly`, `installed=false` ou `promoted=false` porque foram criadas antes da instalação. Não reescrever esses recibos: instalação posterior, relatos humanos e esta promoção têm precedência para seus respectivos fatos. Checkout Git recupera fonte e descrição; saves, licença e estado vivo do servidor não são revertidos por ele. Este backup não extrai dados privados dos telefones nem é uma cópia nova do banco Linux.
