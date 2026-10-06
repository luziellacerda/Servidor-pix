# APP → SERVIDOR: R62 integrada e instalada no A56

Data: 06/10/2026. **Entrega do cliente Android ao implementador do Servidor-pix. Este é nosso recibo, não é um novo retorno do servidor.**

## Use esta fonte para a próxima análise

- Repositório: [TurboElden](https://github.com/luziellacerda/TurboElden).
- Branch: `fix/station-r62-integrated-20261006`.
- Commit completo: `087b6823814d1ec6fc3b925dba8045d9b5628f02`.
- [Handoff completo da implementação atual](https://github.com/luziellacerda/TurboElden/blob/087b6823814d1ec6fc3b925dba8045d9b5628f02/versions/station-online-integrated-r62-20261006/HANDOFF-APP-R62-PARA-SERVIDOR-20261006.md).
- [Fontes, receitas, manifestos e evidências](https://github.com/luziellacerda/TurboElden/tree/087b6823814d1ec6fc3b925dba8045d9b5628f02/versions/station-online-integrated-r62-20261006).
- APK R62 SHA-256: `114dba8af4cb2a0a6beec1aff8123959bcc150fbfbd6dd706afa5bf5f6ef54c7`.
- DEX35 SHA-256: `861402ef2fd7c6995110cc9f1abda12465d535629c4d4f78152c5515977e1b94`.

## O que já foi integrado

Retorno do servidor `7c6e1674eaabab4d69d83b022a9660466dd0ec0c` e implementação `f8b019d6c7f3314aebfdc1afd62f4a505154935a` foram incorporados. Os três Java de prontidão TCP/WSS estão byte a byte iguais à entrega do servidor. Canal Binder, saída e runtime preservados. Houve compilação DEX/APK real no Windows, instalação por atualização no A56 e conferência integral do hash. Não é mais somente api-check-only.

Foram acrescentados: capas autenticadas no seletor e durante preparação da sala, criação que só muda de tela após confirmação do servidor, botões finos, capa maior, retirada da moldura interna de Sua sala e da linha SALAS/contador. Não mudou o contrato online nem houve implantação Linux.

## Pedido ao servidor que está preparando novo retorno

Leia a composição R55 + R57 + nove fontes R62 antes de propor alteração. Não usar R41 isolada ou reaplicar uma Activity antiga, pois perderia sessão, saída e visual atuais. Se o retorno em elaboração usa outra base, indique a base exata e entregue delta conciliável, com funções/arquivos, contrato e evidências; não declare que este APK foi testado no Linux.

Confira prontidão, aceitação do convidado e negociação do motor, mantendo autenticação e verificação de mesma ROM/runtime/opções. A criação no cliente agora permanece no formulário em caso de erro. Não alterar a API para mascarar sala vazia nem antecipar connecting sem host-listening.

## Aparelhos e conferência

Samsung A56: R62 instalada, hash conferido e dados preservados. Mantenedor assumiu a conferência visual/funcional. Motorola Edge30: última instalação conferida continua R58; o telefone foi reconectado com StationRetroActivity aberta. O usuário pediu aguardar o novo handoff, integrá-lo e só então continuar a instalação no outro aparelho. Não inferir versão do POCO. Gameplay em dois aparelhos, inputs, senha do motor, latência e retorno físico continuam sem homologação conjunta.

O Windows aguarda retorno novo posterior ao 7c6e167. **Não tratar esta publicação APP → SERVIDOR como resposta a ser integrada.** Nenhuma ordem de migração, restart, mudança de licença ou modificação de outros produtos.

---

## Cópia integral do handoff Android publicado

# APP → SERVIDOR — R62: retorno integrado e correções das salas

## Destinatário e finalidade

Este documento é um retorno do implementador **Android** ao operador/implementador do **Servidor-pix**. Não é uma ordem de implantação de servidor, alteração de regras ou liberação de motores. A implementação recebida foi incorporada no APK. O estado da instalação está no recibo anexo, separado da compilação e da validação de partida.

Retorno consumido: Servidor-pix `7c6e1674eaabab4d69d83b022a9660466dd0ec0c`, arquivo `docs/station-android/RETORNO-ANALISE-APP-R55-STATION-20261006.md`. Implementação: TurboElden `f8b019d6c7f3314aebfdc1afd62f4a505154935a`, pasta `versions/station-relay-readiness-r57-20261006`.

## Identidade e pastas exatas

| Componente | Local/identificação |
| --- | --- |
| APK final | `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R62-20261006.apk` |
| APK SHA256 | `114dba8af4cb2a0a6beec1aff8123959bcc150fbfbd6dd706afa5bf5f6ef54c7` |
| APK bytes | 2.093.292.264 |
| DEX35 SHA256 | `861402ef2fd7c6995110cc9f1abda12465d535629c4d4f78152c5515977e1b94` |
| Certificado original | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |
| Pacote | `org.turboramastation.frontend`; classes Java `org.emulationstation.frontend` |
| Fonte/build final | `E:\ESTUDO APK\work\station-room-space-r62-20261006` |
| Receita local Java/assinatura | `build_java.py` e `package_r62.py` nessa pasta; chave e argumentos de assinatura permanecem privados |
| Integração anterior do retorno | `E:\ESTUDO APK\work\station-online-integrated-r59-20261006` |
| Ampliação das capas | `E:\ESTUDO APK\work\station-room-art-r58-20261006` |
| Nativo preservado | Carrossel R57, SHA `1dd67942358abebca8e82a4c457a9d4163a48b2e73aa856ce62ab78018cde922` |

Não restaurar R41/R55/R57 isoladamente sobre esta edição. R58 e R59 foram intermediárias: R58 instalada/conferida no Motorola; R59 instalada/conferida no Samsung antes dos pedidos adicionais. O Motorola continua precisando receber a R62 para comparar dois aparelhos com a mesma implementação. Não inferir a versão do POCO.

## Fonte completa e ordem de restauração

1. Snapshot R55 `versions/station-current-r55-20261006`, commit `9d3d45f048aa44bb2ee9c41f567e985901628daa`.
2. Overlay R57 `versions/station-layout-r57-20261006`, commit `8980cd422d63068299b5e9946c120f81a9c94f29`.
3. Nove fontes Java desta pasta R62, substituindo os homônimos e adicionando os novos. Os161 hashes finais do compilador foram conferidos contra essa composição.

O manifesto identifica arquivos, tamanhos e hashes. Dependências originais: Android34 `6cea1df3…`, station-client.jar `e4185497…`, D8 `d43c8a94…`, conforme recibo de integração R59. A compilação final produziu DEX de verdade, sem stubs/API-check-only. Para repor as161 fontes Java, use `recipes/restore_sources.py E:\ESTUDO APK\work\nome-novo` a partir deste snapshot. Ele recusa destino existente, verifica cada hash e copia as receitas para a raiz do novo build. Depois execute build_java.py e package_r62.py. A receita publicada requer STATION_KEYSTORE, STATION_KEY_ALIAS, STATION_KS_PASS e STATION_KEY_PASS no ambiente, preservando o certificado esperado. A fonte C++/mídia/assinatura privada continua identificada pelos manifestos externos R55/R57; nenhum substituto vazio foi criado.

O build Java usa a pasta como raiz, duas árvores `netplay-src`/`dependency-src` e `build/`; temporários em E:. O empacotamento final usa exatamente o APK R61 hash `6a5e6598dbf923e084be25aca500f3c156955c748d8d8ddd155f95d66d7d8b84` e troca somente classes35.dex. Assinatura original, alinhamento16KiB e todas as13.193 demais entradas foram conferidos. Para reconstruir a cadeia completa, R59 é R58 + os três arquivos entregues pelo servidor; R58 é R57 + duas alterações de apresentação das salas. O APK final não depende de executar as receitas antigas com base incorreta.

## Mapa do código e comportamento

| Arquivo | Responsabilidade final |
| --- | --- |
| StationHostConnector | Esperar TCP127.0.0.1 e conservar a primeira conexão útil; prazo45s; JNI como dica |
| StationRelayTunnel | Exigir TCP+WSS antes de ready; pin/protocolo/ticket; cancelamento e fechamento |
| StationRetroActivity | Canal normalizado, evento3, saída idempotente, Voltar/HUD; não anunciar prontidão após encerrar |
| StationRoomsActivity | Seções, seleção, criação confirmada, capa, botões e mensagens de erro na tela correta |
| StationCreateGameCard | Ações à esquerda/capa à direita; altura disponível; FIT_CENTER; alternativa vertical em largura estreita |
| StationRoomArtwork | Cache persistente autenticado, miniaturas e imagem principal, duas tarefas e fila limitada; descarte ao sair |
| StationRoomCreation | Confirmar roomId/itemId/hostId/selfId/membro antes de navegar para a sala |
| StationFlowPanel | Texto/controles roláveis à esquerda e capa proporcional à direita nos diálogos |
| StationPlayerSheet | Perfil com miniatura do jogo da sala ou do convite, sem reter capa antiga ao trocar |

### Conexão da partida

Os três arquivos do retorno `f8b019d6` estão byte a byte preservados na R62. TCP real e WSS precedem evento3; StationGameSession envia host-listening; a API muda starting→connecting; só então o convidado é elegível. Não antecipamos o convidado nem suprimimos verificação de ROM, engine, runtime e opções. StationSessionChannel/StationGameSession e o HUD R54/R55 permanecem. O servidor não precisa de nova publicação para esse delta.

### Capas e seleção

Criar sala ganhou capa maior usando a altura do painel; Sua sala também mostra a capa. A lista Escolher jogo agora tem miniaturas, nome e plataforma. O fluxo antigo só lia o arquivo se já estivesse no cache: jogos nunca vistos podiam ficar sem capa. Agora `StationRoomArtwork.load → StationCoordinator.cover(itemId,cancel)` consulta o mesmo cache e, quando necessário, usa o GET de capa autenticado já implementado pelo cliente Station. Não inventa URL/coverId nem usa CDN legado. A imagem validada fica no telefone para reutilização.

Duas tarefas de imagem, fila24, requisições simultâneas do mesmo cover/revisão/tamanho unificadas, cache de bitmaps8MiB; miniatura até128px e capa até1024px, sem corte/distorção. As duas tarefas existentes podem terminar após cancelamento cooperativo, mas não atualizam a tela quando o epoch mudou. onStop cancela requisições e esvazia fila; onDestroy encerra executor/cache. Não há timer/repetição, vídeos ou trabalho de imagem mantido em tela oculta. Bitmap compartilhado não é reciclado manualmente enquanto há views usando-o.

### Capa em cada etapa e aproveitamento da célula

R62 remove a célula interna de Sua sala e a linha separada SALAS/contador. Capa e controles ficam diretamente no painel principal. Se houver apenas sua sala, ela usa a altura restante; convites ou outras salas mantêm a lista rolável. R62 usa zero margem vertical interna em Criar sala, distância lateral6dp entre conteúdo e arte, e a altura restante do painel. FIT_CENTER conserva o arquivo inteiro e sua proporção: não estica uma capa vertical até virar uma imagem horizontal. Aumentar além da altura útil cortaria a imagem; a área dos botões usa o espaço horizontal restante.

Escolher jogo, Código da sala, Entrar por código, confirmação de mudança de sala, confirmação de início e perfil exibem capa. Convites recebidos e cards de outras salas carregam a capa por seu próprio itemId; não reutilizam incorretamente a capa do cabeçalho. Ao confirmar entrada em outra sala, a imagem usa explicitamente o itemId de destino. Sem jogo selecionado/conhecido ou se o arquivo real falhar, há indicação de capa indisponível, nunca arte inventada.

A tela nativa do emulador após iniciar permanece intacta. A capa acompanha a preparação na Activity das salas; não há sobreposição de capa em cima da partida nem progresso de carregamento fabricado. Os diálogos de preparação usam largura até740dp e altura até440dp limitadas pela tela, rolagem dos controles se necessário e imagem inteira ao lado.

### Botões e criar sala

Os botões de Criar sala/Sua sala têm face mais fina com margens internas na arte; o alvo de toque mínimo continua48dp. Sua sala organiza ações em duas colunas junto da capa. Não mudou a faixa INSTALADO nem a barra vermelha; o nativo da barra superior R57 foi preservado.

Defeito de navegação comprovado no cliente anterior: o listener chamava navigate(0) antes de createRoom. Qualquer falha na preparação do arquivo ou no POST deixava o usuário no lobby vazio, com mensagem apenas no rodapé. Agora congela o item escolhido, impede nova escolha durante a solicitação, valida o arquivo/motor e espera o POST create. Só navega quando a resposta contém a sala desse item, do próprio anfitrião, com sua participação confirmada. A confirmação também é reavaliada no estado atual antes da troca de tela.

Erro local, jogo não instalado, incompatibilidade ou recusa HTTP permanecem na tela Criar sala, com mensagem visível e nova tentativa disponível. Não baixa jogos automaticamente, não cria sala falsa e não ignora compatibilidade. A tentativa humana específica de trocar jogo não foi correlacionada a um arquivo/erro identificado; esta revisão não atribui sua causa ao servidor por suposição.

## Comparação com o servidor

Fetch de todos os ramos feito antes da correção solicitada. Último retorno visto:7c6e167; fonte entregue:f8b019d6. `src/TurboRamaSuiteOnlineServer` foi comparado entre a API publicada a2bb176 e a branch remota atual: zero diferenças. Alterações posteriores de cadastro estão no serviço administrativo, não na API online. Não foi feita nova inspeção Linux de PID/binário nesta etapa Windows.

Continuam os contratos POST `/v1/station/online/command`, POST `/v1/station/online/events`, WSS `/v1/station/online/relay`, envelope assinado e sessão por aparelho. O comando create continua recusando participante que já pertence a uma sala e exigindo jogo/motor autorizado. Este retorno não pede migração, alteração de chave, limite, autenticação ou outros produtos.

## Testes e alcance das evidências

- 158Java compilaram para R59;160 para R60;161 para R61/R62, API34/Java8/D8min26 com dependências originais.
- 10 verificações TCP reais executadas neste Windows: listener tardio semJNI, socket retido, bytes ida/volta, ausência de falso pronto, prazo/cancelamento.
- 255 verificações das regras de salas/comunidade executadas neste Windows.
- 454 verificações Parcel/ResultReceiver executadas novamente no A56; reproduziram a falha antiga e passaram com o canal normalizado. O primeiro wrapper terminou com erro ao copiar a si próprio depois do teste; essa cópia foi retirada e a execução completa foi repetida com sucesso.
- 14 verificações executadas novamente sobre a classe final R62, sem alteração de política desde R60: confirmação de criação: resposta vazia, jogo antigo, anfitrião/membro incorreto e confirmação válida.
- Os 39 testes de TLS/relay e12,58MB/direção do servidor são evidência recebida, não repetida no Windows nesta rodada.
- Assinatura, alinhamento e preservação de todas as entradas não alteradas passaram em cada APK.

## Reprodução do código publicado

As 161 fontes foram repostas em uma pasta nova de E: usando apenas a composição indicada no Git e o manifesto final. O Java/D8 foi executado novamente e produziu exatamente o mesmo DEX SHA-256 da R62 instalada. Consulte `evidence/rebuild-from-published-sources.json`. As dependências binárias originais e o APK base permanecem requisitos explícitos; não é um build independente de todo o APK.

## Validação física e próximos passos

A R62 foi instalada por atualização no Samsung A56; o SHA-256 integral lido do APK no telefone coincide com o artefato de build. Não houve desinstalação nem limpeza de dados. Na etapa R61, foram observadas a capa em Sua sala e a capa grande no diálogo Código da sala. **O mantenedor assumiu a conferência física após a instalação R62. O implementador não concluiu a revisão visual R62 nem a criação/troca de jogo ponta a ponta no aparelho.** A configuração temporária de manter a tela acesa foi restaurada e conferida em 0. Sem comandos posteriores no telefone.

Consultar `evidence/installation.json` e `evidence/visual-check.json` para os fatos observados; não confundir compilação com instalação ou teste visual. Fotos/logs brutos e identificadores pessoais permanecem privados.

Observação transitória anterior, com R60: uma captura exibiu o teclado nativo do RetroArch pedindo senha do servidor; a captura seguinte estava em Criar sala e houve interação concorrente do usuário. Não foi estabelecida a causa nem aplicado bypass de autenticação. A configuração existente continua transferindo a senha assinada ao runtime. Correlacionar se esse sintoma reaparecer; não declará-lo corrigido por causa do layout.

Depois de atualizar também o segundo aparelho: sala nova, mesma edição de Battletoads, dois membros, ambos Pronto, anfitrião Iniciar. Correlacionar UTC/aliasA-B, APK/DEX, sala/geração/papel, TCP/WSS, host-listening-ack, estado connecting do convidado, bytes/inputs reais e saída/retorno. Exercitar também cancelamento, perda de rede e segundo plano. O comportamento recebido encerra o relay em onStop; não há retomada transparente prometida.

Sem nova prova de gameplay em dupla, latência, aquecimento ou controles online próprios. O online continua usando RetroArch/bsnesMercury/ClownMDEmu; o local preserva os emuladores próprios. NeoGeo online continua condicionado ao launchReady do manifesto. Não declarar versão estável geral a partir de testes isolados ou sala criada.
