# TurboStations R135 — kit atual e aceitação do servidor

Esta atualização continua o [mesmo handoff de R134](../r134/README.md). A referência executável agora é **R135**, fonte [`ca8f1ccda18bef397e71e1c979a4f813acf608c0`](https://github.com/luziellacerda/TurboElden/tree/ca8f1ccda18bef397e71e1c979a4f813acf608c0/integration/online-lifecycle-review-r135), APK SHA256 `f6abe89490e7ba5e2cdd432625888bed6f40aea115d730cf612d509ec6ca9896`, **2.714.991.631 bytes**. A revisão foi compilada, assinada com o certificado original v2/v3, alinhada a 16 KiB e instalada no Motorola Edge 30 em **12/10/2026 às 04:33:05 UTC**; frontend oficial observado às **04:33:07 UTC**, com dados preservados. Em Fortaleza, os horários são 01:33:05 e 01:33:07 de 12/10. Esta operação atualizou somente esse aparelho.

O [DELIVERY.json](DELIVERY.json) registra os novos quatro IDs, os hashes do kit e as condições de aceitação. Os [recibos públicos R135 do aplicativo](https://github.com/luziellacerda/TurboElden/tree/build/station-online-exit-r135-20261012/versions/station-online-exit-r135-20261012) contêm README, STATUS e evidências sanitizadas, com os hashes dos originais separados. O kit privado está preparado localmente. Recebimento pelo operador Linux, cadastro/ativação, três jogadores pela rede e saída física confirmada continuam sem recibo novo. O último retorno operacional examinado no servidor permanece o [retorno em a6b58e9 que revisa R129](https://github.com/luziellacerda/Servidor-pix/blob/a6b58e9beb12eb564c1b58d9d4c32ed3e7513861/docs/station-android/online-plataformas-20261009/r128-retorno-servidor-20261011.json).

## Alteração do aplicativo e composição

R135 incorpora apenas a correção da ordem dos monitores na saída humana do Dolphin e a confirmação da saída v3 após rejeição comprovada de geração antiga. As fontes e os testes locais dos incidentes estão na integração R135. Falhas HTTP ou de rede ambíguas não autorizam repetição cega de Leave; o cliente mantém a categoria de saída não confirmada. Não foi acrescentada persistência durável de intenção de saída sem rede.

Sobre o donor R134 `f8cb09184f41bc7c942ce84541dbd90d575b7240467ce0033d35be65e57caee2`, o pacote contém **cinco substituições, zero adições e 13.855 entradas preservadas**, total de 13.860. Somente `classes35.dex` muda como payload de comportamento; os três marcadores e `assets/station-online/engines.json` renovam o vínculo com esse bundle. O DEX passa de 606 para 608 classes: 582 classes externas exatas, 24 para 26 tipos permitidos, duas adições e nenhuma remoção. Os outros 39 DEX da raiz, DEX privado PS4, Manifest, recursos, frontend, motores, mídias, opções e perfis são preservados. A renovação dos IDs não constitui nova aprovação de gameplay.

## Quatro identidades derivadas do pacote final

| Plataforma | Engine ID R135 | Perfil preservado | Teto do motor |
| --- | --- | --- | --- |
| psx | `pcsx-rearmed-ff81ed17-player-v3-841204679ef2` | standard-2p-v1 | 2 |
| n64 | `mupen64plus-ae-33bf7021-n64-v3-de303362cf1c` | direct-four-ports-v1 | 4 |
| gamecube | `dolphin-5102a033-gamecube-v3-76ce6c2190d4` | dolphin-gamecube-four-ports-v1 | 4 |
| wii | `dolphin-5102a033-wii-v3-76ce6c2190d4` | dolphin-wii-remotes-four-v1 | 4 |

GameCube e Wii compartilham o marcador Dolphin; seus IDs e perfis continuam distintos. Os IDs R134 não são aliases válidos da R135. Todos os participantes precisam corresponder aos mesmos engineId, coreSha256, runtimeSha256, identidade completa do conteúdo, perfil e configuração canônica. Atualizar um telefone não comprova a compatibilidade do segundo ou terceiro aparelho.

## Kit privado e arquivos originais

O hash SHA256 do manifesto privado de preparação é `3e946079b74f1ca75d02a48b63cf708f9d56bcfdff62547c0bcc3912cb247853`. O inventário público é um mapa de nomes, tamanhos e hashes, sem bibliotecas, APK, originais privados, credenciais ou identificadores de telefone. Esses metadados permitem comparar o kit recebido; este documento não é um par identidade/pacote utilizável para implantação.

O conjunto conserva a organização da R134:

- `server-kit`: seis bibliotecas reais — os cores PCSX-ReARMed, Mupen64Plus e Dolphin, mais os três runtimes — e `artifact-kit-receipt.json`, `incoming-engine-manifest.json` e `profile-canonical-receipt.json`.
- `server-kit-original-receipts`: `package-final.json`, `runtime-final.json`, `source-freeze.json`, `engines.json` e `psx-controller-profile.json`, todos copiados byte a byte dos originais finais R135. Os dois recibos identidade/pacote devem permanecer originais; cópias públicas sanitizadas não satisfazem o selo.
- `server-kit-tools`: os mesmos `extract_private_server_artifacts.py` e `build_incoming_manifest.py`, fonte do servidor `a6b58e9`, hashes `27053b843d763adcf570e859b2c8658e216dbf6f80963b600fa5326958f70ef4` e `362bcae9d8915f7f43aac4c48d06b35bdf57efc88d6acb9199aa06e3a0231426`. Nenhum desses helpers foi modificado.

São 16 arquivos no inventário, além do manifesto de preparação. O extrator conferiu o APK completo, a igualdade do selo original de identidade, as seis bibliotecas e as 20 dependências. Os perfis canônicos e opções mantêm os hashes da R134. APK, firmware, jogos e chaves não foram copiados para o kit. Nenhum arquivo privado desse conjunto integra o Git.

## Condições para administração e ativação

1. Receber o kit privado pela via autorizada e conferir os hashes de todos os arquivos, o par original identidade/pacote, o APK R135 e os quatro IDs. Usar os recibos originais completos e os baselines ativos atuais do servidor.
2. Utilizar o registrador v3, o merge de modos por escopo e a transação já existentes, com autenticação administrativa nativa, selo/lock/rollback e preservação dos registros antigos. O cadastro legado de engines v2 e a lista antiga `snapshot.engines` não demonstram ativação v3. Os dados C# têm a recarga existente; não há pedido de reinício ou implantação automática neste handoff.
3. Revalidar a matriz preparada em R134 sob os IDs R135 por edição, conteúdo completo, owner, perfil e modo. A antiga matriz é evidência de preparo; não aprova automaticamente os novos vínculos. Não ampliar portas, opções ou aprovações PSX/HLE. PS3 e PS4 continuam exclusivamente offline.
4. Devolver uma resposta assinada fresca de `POST /v1/station/online/multiplayer/command` vinculando os quatro runtimes reais e a matriz única de aprovados/recusados, com razões. Distinguir engine cadastrado, modo aprovado, admissão da sala, gameplay e publicação de capas.
5. Para o diagnóstico de barreira, a fonte qualificada está em [`1adcc732`](https://github.com/luziellacerda/Servidor-pix/tree/1adcc732455da0887dfecb901a8a94bdf79f258d), [PR 5](https://github.com/luziellacerda/Servidor-pix/pull/5). O [incidente e a prova de domínio](../r134/INCIDENTE-TRES-JOGADORES-E-SAIDA.md) permanecem válidos. Publicação e CI não comprovam que esse diagnóstico foi implantado em produção.

## Aceitação obrigatória com três jogadores e saída

Escolher uma edição/modo de N64, GameCube ou Wii que aprove explicitamente três jogadores. `maximumPlayers=4` não basta: um modo com `allowedPlayerCounts=[2,4]` recusa três. PSX mantém duas portas padrão e não é candidato de três pessoas nesta revisão.

Na mesma sala e no mesmo epoch, host mais dois convidados formam **dois links e quatro attachments**. Antes de Playing, os quatro attachments devem estar presentes e confirmar HELLO, PAUSED e READY; os bytes pendentes de replay devem ser zero e os participantes não podem estar suspensos. Guardas de licença, conteúdo, configuração, ACK e geração devem continuar ativos. O teste de domínio anterior demonstrou que falta de READY com PING/heartbeat saudável pode manter a sincronização em espera; R135 não acrescenta prazo geral nem força a saída de pessoas para ocultar essa condição.

Registrar uma partida real com os três aparelhos e depois saída humana do host e de convidado. Conferir o snapshot próprio fresco e a ausência da sala antiga, a saída local sem travamento/diálogo e a preservação do comportamento de encerramento da sala no servidor. Se ocorrer `GENERATION_MISMATCH`, o retry R135 exige snapshot próprio assinado, mesma sala e o mesmo binding original de protocolo/item/engine/core/runtime/conteúdo/perfil, com geração fresca válida. HTTP 500, falha de rede ou outro resultado ambíguo não autorizam repetir mutação. Falha de rede deve permanecer identificada como saída não confirmada.

Os 1.452 checks de domínio do servidor, incluindo 29 novos, continuam provas sintéticas de classes reais; não são uma partida Android/WAN. A instalação e a observação do frontend R135 também não homologam três jogadores, controles, saída online, desempenho ou térmica. O retorno requerido deve trazer aplicação dos dados, prova assinada atual, resultado da partida e resultado das duas saídas, sem tokens, tickets, IDs pessoais ou payload de jogo.
