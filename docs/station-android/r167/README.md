<!-- STATION-PSX-ANDROID-XML-20261015 -->
## Atualização: falha Android R167 capturada e fonte corrigido

O Motorola conectado está na R167 pelo hash independente do APK. O erro de
Capcom VS foi capturado em `StationPsxLaunch.copyPresentationSettings`: a
fábrica DOM do Android rejeita uma feature antes da conexão do motor.
O método corrigido passou em 29 verificações no próprio Motorola e 64 JVM;
o arquivo real de preferências permaneceu intacto. O novo APK ainda precisa
ser compilado e assinado pela produção. Reinstalar a mesma R167 não inclui
esta correção. A indicação anterior de instalar R167 abaixo é histórica.

Ler [incidente, contrato e provas](diagnostics/psx-android-xml-20261015/README.md)
e `diagnostics/psx-android-xml-20261015/build-contract.json`. Compilar a partir
da R167 exata e fonte bb045d81, preservar 39 DEX e 607 classes de DEX35,
alterar StationPsxLaunch e incluir StationPsxLaunch$1. Emitir prova final/kit
exato e publicar o instalador USB permanente para instalação manual.
Não repetir a publicação R167 já aplicada nem alterar os impedimentos Crash.
Servidor não foi reiniciado ou alterado para este reparo do cliente.

# R167 — cadastro aplicado e consumo público conferido

O kit privado `82cf0bff` foi verificado neste Linux:48 arquivos, 47 hashes e 27 ELF
ARM64. Núcleos e 21 dependências iguais à R165/R166; históricos de fontes nativas,
opções e controles conferidos. Os marcadores novos vinculam o carrossel R167.
As 40 DEX, a correção PSX `libc++_shared.so` e a ponte N64 são preservados segundo
os recibos da produção. Nenhum APK recebido ou assinatura verificada aqui.

**Cadastro aplicado:** quatro motores e 492 vínculos; catálogo 100 com 4.744 itens,
87 motores, 16.404 perfis e 11.489 modos. Baseline fresca83/15912/10997 conferida por
autenticação Linux, backup do registrador vigente e `scan.lock`. R166 não estava
aplicada e foi substituída diretamente pela R167; não executar seu publicador.
As instruções de seis vínculos novos WinBack foram condensadas para respeitar
o orçamento existente, sem mudar controles, contagens, fontes ou linhas antigas.

PSX: 84 edições, 93 decisões, 23 modos para duas pessoas em 19 edições, 17 impedidos e 53 locais sem
vagas. N64: 157 edições, 387 perfis, 230 modos multiplayer e 157 individuais; GameCube nove modos, Wii três.
As decisões foram requalificadas por identidade completa, usando o ledger real
do mesmo core/opções. Não há aprovação por nome ou `metadata.players`.

Capabilities assinadas locais e públicas das 84 edições PSX e 157 N64, 23 admissões/167
recusas PSX e 230 admissões/245 recusas N64 passaram. Os quatro owners foram
conferidos; GC/Wii tiveram 12 admissões e recusa da quinta vaga. Transporte
PSX com dois clientes e snapshot real de 4.456.448 bytes; N64 com quatro clientes e seis WSS passou com TLS/SPKI,
prioridade, pausa/retomada e limpeza das contas próprias. São clientes de
diagnóstico, sem execução Android ou quatro pessoas homologadas.

DLL53d4/fontec0ca229/PID152812/NRestarts0 preservados; **zero reinícios**.
A saída confirmada com o owner N64 R167 entregou STATE3 antes de close1000
nos dois participantes, repetindo o mesmo leave três vezes. Nenhuma lógica N64 ou
implantação R165 foi reaplicada. Catálogo, capas, ROMs, saves, downloads,
licenças reais, esquema, outros serviços e rede 100/800 preservados.

## Como o app deve consumir

Use a autenticação/prova e verificação de assinatura existentes. Envie POST para
`https://app.lzgames.com.br/v1/station/online/multiplayer/command`, com
`action=capabilities`, `requestId` UUID novo, `itemId` exato e
`clientMaximumPlayers=5`. Esse5 é o teto negociado do cliente e não libera cinco
vagas em um jogo. Confira requestId/licença da resposta, assinatura,
`snapshot.multiplayerVersion=3` e `snapshot.capability=station-multiplayer.v3`.
O subprotocolo WebSocket do relay é `station-stream.v3`.

Escolha o perfil pelo engineId exato desta instalação em `exact-owners.json`.
Compare itemId, conteúdo, core, runtime, profileId e profileSha256 completos;
exija `approved=true` e a capacidade solicitada em `allowedPlayerCounts`.
Perfis de motores anteriores continuam disponíveis: não escolher a primeira
linha por nome. `create` usa essa mesma tupla e a capacidade; modos locais
`maximumPlayers=1`/contagens vazias não criam vagas online. Reutilize requestId
somente para repetir a mesma operação/corpo conforme o contrato existente.

## Piloto físico da produção

Instalar manualmente o mesmo APK assinado R167 nos dois aparelhos pelo instalador
USB permanente da produção, preservando os dados. Hash do APK esperado:
`dd6f8f8131cf5ee55d450c60e0b08963a662dc6cad092588f0db50153badd978`.
Não atualizar o telefone automaticamente nem enviar APK ao Git.
O manifesto público do instalador ainda aponta R165. A produção deve confirmar
seu manifesto privado R167 e, se necessário, publicar o APK e a prova final
pelos caminhos exatos no publicador permanente. Este Linux não inspecionou o
manifesto privado do Windows nem o alterou.

Na seleção, confirmar apenas vizinhas 10% mais largas. Para PSX, usar Capcom vs.
SNK — Millennium Fight 2000 Pro, item `station_767e5e92f2f353479e6120f1c28f96a7`,
conteúdo `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`,
modo VS/`versus`, duas vagas. Registrar separadamente hash instalado, primeiro
quadro, áudio, controles e saída do anfitrião/retorno do convidado por 25 segundos.
R167 e gameplay PSX Android ainda não estão homologados. R164 estável preservada.

## Retomada e evidência

Ler `server-return.json`, `profiles-r167.json`, CSV das 84 edições e `evidence/`.
Autoridade privada: `/mnt/DADOS/station-r167-git-20261015/completed.json`.
Não repetir cadastro, provas completas, DLL ou capas por leitura deste retorno.
O rollback de serviço R165 preservado exige retirar seus DOIS drop-ins próprios
para selecionar b347; não há drop-in ou DLL de serviço R167 para desfazer.
Não restaurar registros antigos sobre dados novos; conciliar baseline e backups
do registrador antes de qualquer reversão. As demais pendências globais estão
mantidas no estado do monitor e no mesmo handoff principal.
