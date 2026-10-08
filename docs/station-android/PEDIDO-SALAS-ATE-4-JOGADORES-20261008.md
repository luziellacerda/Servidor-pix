# Pedido APP → SERVIDOR: salas de até quatro jogadores

Solicitado pelo mantenedor em 08/10/2026. Auditoria do app publicada em `f8e8779737ae2cf3280b371eec4a6efc17f8e605`.
[Plano e evidências no app](https://github.com/luziellacerda/TurboElden/tree/f8e8779737ae2cf3280b371eec4a6efc17f8e605/docs/server).

**R76 já instalada e hash integral conferido no Samsung A56 e Motorola Edge 30.** Fonte executável permanece `2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`; APK `d7145db3511a4056b16fdde10e4c07709a16b7f445596801b3535c1b28b06a51`. Licença/jogos/saves preservados, UID/data original conferidos. Sem nova alegação de gameplay prolongado.

Este pedido é uma auditoria e proposta para quatro participantes, **não habilitação já implementada**, resposta do operador ou implantação Linux. Preservar o funcionamento de duas pessoas e a correção R76. O app atual não enviará campos inventados ao contrato estrito.

---

# Salas de até quatro jogadores — auditoria e contrato proposto

## Estado e escopo

Solicitação do mantenedor: manter partidas de duas pessoas e permitir até quatro pessoas nos jogos que oferecem essa modalidade, tendo Bomberman como exemplo. Cada pessoa pode estar em um telefone e rede diferentes. O limite deve ser específico do jogo, da modalidade e do motor.

Esta é uma análise APP → SERVIDOR e uma proposta para a próxima implementação. **A R76 não habilita quatro jogadores.** Nenhuma alteração de sala, motor, protocolo ou servidor foi aplicada por esta auditoria. Não anunciar quatro vagas antes de os controles e o transporte estarem qualificados.

Base app: fonte executável R76 `2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`; recibo documental `7d903e64d7d059080cfc5102b36e1b68d7293bf5`. APK `d7145db3511a4056b16fdde10e4c07709a16b7f445596801b3535c1b28b06a51`. Mantém a correção APP-01 e os vídeos Neo Geo da R75.

Último retorno real do servidor: `ed9ca9fdcc16f3b9f86792d43ed01d5812a9f1d8`. Produção declarada nesse retorno: `ab192bf1585e30f303d041f13b36a1f9c96d2caa`. A candidata `6f27c6ca176b80da734a0000d6f07f72a480e5ed` não tem ativação comprovada e mantém o limite de dois. O commit `a9f7f41b243f62e7ff5c3240d65eb7704034184c` é nossa entrega R76, não um novo retorno do operador.

## Fontes oficiais e jogo piloto

- A [documentação de múltiplos controles do Libretro](https://docs.libretro.com/guides/netplay-multiple-controllers/) usa **Super Bomberman 2 de SNES** como exemplo de quatro jogadores. Explica a configuração de Multitap e a atribuição de controles pela rede. Seu exemplo de dois computadores com dois controles cada não comprova quatro telefones na TurboStations.
- A [documentação do bsnes-mercury Performance](https://docs.libretro.com/library/bsnes_mercury_performance/#multitap-support) anuncia Multitap na segunda porta. Essa declaração geral precisa ser confrontada com a versão exata compilada.
- A [arquitetura de netplay do RetroArch](https://docs.libretro.com/development/retroarch/netplay/) mantém conexões individuais dos clientes com o anfitrião, sincroniza entradas e exige versões e conteúdo compatíveis.

Proposta de primeiro título para qualificação: **Super Bomberman 2, SNES, modo Battle**, identificado por conteúdo/versão verificados. Não liberar por busca textual de “Bomberman”: outros títulos, plataformas, regiões, modificações e modos podem ter limites diferentes. Não tratar jogadores alternados como quatro controles simultâneos. A escolha desse piloto não afirma que a variante correta já esteja no catálogo ou testada.

## Bloqueios atuais no app

Fontes Java conciliadas da R76: `E:\ESTUDO APK\work\station-pump-wakeup-r76-20261008\compiled-final\java\netplay-src\org\emulationstation\frontend\netplay`.

| Arquivo | Evidência | Alteração necessária |
|---|---|---|
| `StationRoomRoster.java:14,18,105` | Dois lugares mínimos; a posição visual não representa porta do controle | Capacidade assinada e relação explícita participante/controle; não inferir porta pela ordem da lista |
| `StationRoomStartState.java:12` | Exige exatamente dois membros | Conferir a lista congelada para o início e prontidão de todos os participantes |
| `StationRoomsActivity.java:325,377` | Apresentação em duas colunas; lista pública já lê `maximumPlayers` | Grade compacta de até quatro nomes, status e vagas; início não deve ocultar P3/P4 |
| `StationRetroLaunch.java:34` | `netplay_max_connections=1`, dispositivos P1/P2 padrão | Três convidados para uma partida de quatro pessoas e perfil de controles verificado |
| `StationRecoveryTunnel.java:20–26,63–64` | Um socket local, um transporte remoto e um par de filas | Conexões independentes para cada convidado, mantendo escritor único por conexão |
| `StationRetroActivity.java:34` | Cria um túnel | Gerenciar os canais necessários no anfitrião e a espera coletiva |
| `StationGameSession.java:95–105` | Um descritor/ticket renovado | Renovação vinculada ao convidado/canal correto |
| `StationOnlineGame.java:18` | Hash de opções cobre `core-options.cfg` | Incluir perfil dos dispositivos/controles na identidade de compatibilidade da partida |

O upstream nativo comporta vários clientes, mas a integração Station limita a um convidado. Na base RetroArch `69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`, `network/netplay/netplay_frontend.c:2644–2659` aplica o limite de conexões; `:1851–1867` negocia dispositivos; `:4902–4959` atribui portas. A condição Station de prontidão em `:8991–8992` considera conexão com mais de um jogador e também precisa representar a quantidade real esperada.

### SNES: verificar a ponte de entrada antes de habilitar Multitap

Fonte auditada: bsnes-mercury `79d7f9de218b6ffa65a80bbdc5828532bc239232`, em `E:\ESTUDO APK\work\station-netplay-20261004\upstream\libretro--bsnes-mercury-79d7f9de218b`.

`target-libretro/libretro.cpp:575–586,654–656` anuncia e conecta Multitap. Porém `:223–230` retorna zero quando o ID de botão excede 11. `sfc/controller/multitap/multitap.cpp:23–24` solicita IDs calculados como `pad * 12 + botão`, e `emulator/interface.hpp:74` encaminha esse ID. Os pads adicionais do adaptador encontram esse bloqueio. É necessário corrigir o mapeamento e testar P1–P4; a declaração genérica de suporte na documentação não basta.

A procedência foi conferida sem executar jogos: o wrapper local tem SHA `90bfb5826f9e2e85a05d11137eeeebdbc05359b7eba3632d4296e87c5dae007a`, idêntico ao arquivo do ZIP upstream fixado. O core do APK R76 tem SHA `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b`, e sua seção `.text` coincide com o ELF da compilação com símbolos. A função `Callbacks::inputPoll`, endereço `0x14b924`, contém comparação com 11 e retorno zero para IDs superiores. O bloqueio está, portanto, também no código de máquina empacotado. Isso é análise estática; não houve execução de core/ROM nem teste físico de quatro controles nesta auditoria.

Qualquer core corrigido deve receber hash/identidade novos e cadastro correspondente. Não reutilizar a identidade do core atual `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b` para conteúdo binário diferente.

### Mega Drive: perfil específico do jogo

ClownMDEmu `d43c2708b0a31c285ce16724b6c4a2e92af07346`, `source/options.h:380–399`, oferece `clownmdemu_input_protocol` com `standard` (dois), `sega` (oito) e `ea` (quatro). `source/libretro-interface.c:655–670,697` aplica a escolha; `:363` lê a entrada por jogador. O manifesto R76 usa opções vazias e, portanto, o padrão de dois. Não mudar para `ea` ou `sega` globalmente: usar o protocolo compatível com cada título e incluí-lo na compatibilidade assinada.

## Bloqueios atuais no servidor

Arquivos em `src/TurboRamaSuiteOnlineServer`, commit `ab192bf1585e30f303d041f13b36a1f9c96d2caa`.

| Arquivo/linhas | Evidência |
|---|---|
| `StationOnline.cs:14–21,40–59` | Motor não declara capacidade; comando e sala não modelam slots de controle |
| `StationOnline.cs:135,311,413,426` | Entrada e pedidos recusam o terceiro membro |
| `StationOnline.cs:239–250` | Lista pública publica capacidade dois; sala selecionada não informa capacidade |
| `StationOnline.cs:329–349` | Início exige exatamente dois membros e dois prontos |
| `StationOnline.cs:364–381,464–485` | Credenciais vinculadas a membro/geração, papel booleano anfitrião/convidado |
| `StationOnline.cs:501–504` | Presença consulta anfitrião e primeiro convidado |
| `StationOnline.cs:181–190` | Saída de qualquer membro depois do início encerra a sala inteira |
| `StationRecoveryRelay.cs:82–95,115,153–196` | Duas conexões/filas; lado `host ? 0 : 1`, destino `1-side`, pausa/prontidão de duas pontas |
| `StationRecoveryRelay.cs:10–29` | TSR2 não identifica canais ou slots de jogador |
| `StationRelay.cs:12–16,38–40,53` | Transporte anterior também modela uma dupla |
| `StationLibrary.cs:15–16,202–203` | `metadata.players` é texto descritivo, insuficiente para autorizar quatro jogadores |
| `StationOnlineRegistration.cs:25–28` | Integração com catálogo consulta a plataforma do item, sem perfil multiplayer |

**Não enviar campos novos ao serviço atual:** `Contracts.cs:51` proíbe campos JSON desconhecidos; `StationOnlineEndpoints.cs:78,151` usa esse contrato. Acrescentar hoje capacidade/slot ao comando produziria erro 400. A capacidade precisa ser negociada por uma extensão assinada e versionada antes de o app emitir novos campos.

## Comportamento proposto para o produto

1. Ao selecionar o jogo, exibir o limite online efetivamente qualificado. Jogos de dois permanecem com duas vagas; jogos de três ou quatro podem oferecer esse limite; títulos sem perfil confirmado não ganham vagas extras por inferência do nome.
2. Na criação, permitir escolher o máximo da sala entre dois e o limite validado, limitado a quatro nesta etapa. Mostrar vagas e participantes numa única área compacta, com nome, avatar, P1–P4 e prontidão.
3. O anfitrião pode iniciar com pelo menos dois participantes e no máximo a capacidade escolhida, desde que todos os presentes estejam prontos e o modo do jogo aceite a quantidade. Não exigir quatro pessoas só porque a sala comporta quatro.
4. Congelar a relação membro/controle para cada início. Todos recebem o mesmo conjunto de conteúdo, core, perfil de controle, geração e participantes; cada aparelho controla exclusivamente sua porta atribuída.
5. Para queda de rede, exibir quem está ausente e aguardar reconexão com o mesmo lugar. Não remover automaticamente uma pessoa nem trocar P3 por P2. Saída voluntária e encerramento pelo anfitrião devem ter regras explícitas; não implementar migração de anfitrião durante partida nesta primeira etapa.
6. Não permitir ingresso tardio nesta primeira versão: entrar antes da confirmação de início. Testar com cada aparelho como anfitrião em partidas novas, sem prometer migração automática.

## Contrato e arquitetura a fechar com o operador

Os nomes finais de capabilities/campos devem vir do contrato acordado. Os itens abaixo são requisitos, não campos já existentes nem uma instrução para ativar produção.

- Perfil do jogo: conteúdo/versão, modalidade simultânea, mínimo/máximo, dispositivos, opções, revisão/hash e motores qualificados. O servidor valida o limite solicitado.
- Snapshot assinado: capacidade, participantes reais, slot/porta única, prontidão, perfil e geração. Rejeitar slots duplicados ou não pertencentes à sala.
- Lançamento: todos concordam com core/runtime/protocolo/conteúdo/perfil. R76 e clientes anteriores continuam em sessões de duas pessoas; clientes sem a extensão não podem ocupar uma sala de quatro.
- Transporte preferido para estudar: uma conexão confiável e recuperável entre anfitrião e cada convidado, com o anfitrião mantendo até três conexões TCP locais no motor. Pode usar vários WebSockets autenticados ou multiplexação versionada, mas o servidor deve identificar cada canal inequivocamente. **Nunca misturar ou replicar cegamente o fluxo TCP bruto entre convidados.**
- Cada canal tem credencial/prova, escritor, crédito, ACK, offsets e replay próprios. A retomada da emulação é coletiva: não retomar para uma dupla enquanto o terceiro ou quarto participante permanece ausente.
- Presença e saúde por participante; diagnóstico com causa inicial, papel/slot, época e canal saneados. Preservar segurança e limites existentes.
- Revisar reserva de memória e admissão antes de aumentar capacidade. Com três pares e janela de 256 KiB por direção, apenas os buffers de replay podem atingir **1,5 MiB por sala**, contra 0,5 MiB do par atual. São seis conexões WebSocket se forem usados pares independentes, embora existam quatro pessoas. O orçamento global de buffers e o limite de conexões devem ser contabilizados separadamente, sem simplesmente triplicar limites de produção.
- Qualificar a extensão separadamente dos ajustes SRV-01/02/03 ainda candidatos. Não reiniciar produção nem esvaziar salas por causa deste documento.

## Critérios antes da liberação

Plano de testes, ainda não executado nesta auditoria:

- Regressão completa da R76 com duas pessoas.
- Perfis: jogo de dois recusa terceiro; jogo de três recusa quarto; título de quatro aceita P1–P4; capacidade ausente/inválida não libera mais vagas; versões/conteúdos/perfis divergentes são rejeitados.
- Cada entrada P1–P4 aciona somente seu personagem. Validar Multitap do SNES no core exato e protocolo do Mega por jogo.
- Quatro clientes reais no transporte: três streams independentes, tráfego simultâneo, crédito esgotado em um canal sem corrupção nos outros, falha de cada conexão, reconexão/replay sem duplicar bytes, saída e reabertura.
- Segurança: credencial de outro membro/canal/geração recusada, exclusividade de slot, assinatura e compatibilidade preservadas.
- Prontidão e pausa: todos os presentes confirmam; queda de qualquer participante suspende e retoma de forma coordenada, sem iniciar parcialmente.
- Partida física com quatro controles humanos identificáveis; quatro telefones para afirmar especificamente esse cenário. Testes com dois aparelhos e controles extras ou quatro processos no PC devem ser rotulados como tais.
- Coleta de CPU/temperatura/FPS/latência no anfitrião com três convidados e custo real no servidor. Não concluir desempenho com base apenas em testes de contrato.

## Retorno esperado do servidor

Confirmar a arquitetura de canais, o contrato assinado e a negociação com clientes antigos; informar o esquema dos perfis por jogo e as regras de presença/prontidão/saída; apresentar limites de memória e conexões, testes isolados e plano de ativação. Publicar como SERVIDOR → APP com branch/commit e indicação clara de candidato versus produção. Após esse contrato, implementar o app e os cores necessários sem reabrir os problemas já corrigidos na R76.
