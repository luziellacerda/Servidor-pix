<!-- STATION-R169-USER-TEST-SUCCESS-20261016 -->
## Relato de teste recebido — 16/10/2026

O usuário informou: **“funcioou em testes”**, em resposta à entrega da R169.
Resultado positivo registrado no recibo em `userTestReports` e
`userReportedWorkingInTests=true`. Esta mensagem não especifica jogo,
revisão instalada, quantidade de aparelhos nem o comportamento ao sair da sala.

<!-- STATION-R169-APPLIED-20261015 -->
# R169 aplicada e qualificada no servidor — 15/10/2026

O [recibo aplicado](server-return.json) confirma cadastro e provas públicas
da R169: **95 motores, 17.388 perfis e 12.473 modos**, catálogo 100/4.744 itens.
Quatro identidades e 492 vínculos foram adicionados; históricos preservados.
Kit privado `859b33b15669`, 54 arquivos/53 hashes/27 ELF ARM64, conferido.
APK R169 `68521db5…` e assinatura original por prova do produtor; nenhum APK
recebido/instalado neste Linux. A produção relata os dois telefones instalados;
esta entrega não inspecionou seus hashes nem homologou gameplay Android.

## Alterações efetivamente ativadas

Preparador corrigido em `f1c2266`: descrição resumida dos controles no APK e
política completa do servidor são confrontadas por identidade, opções,
configuração e fingerprint. Modos explícitos continuam obrigatórios.
Scanner, registrador e módulo ativo agora concordam em perfis 17 MiB,
modos 9 MiB, manifesto 1 MiB/256 motores; demais limites preservados.
O leitor C# ativo aceita 17 MiB no início e na recarga. Sombra no mesmo sandbox
carregou os **17.294.779 bytes formatados reais** antes da publicação.
Publicados perfis compactos 15.033.038 B, modos 8.469.257 B e manifesto 192.296 B.
Build C# `3781bccd1764f69803a0a777f80f9e632af3f911`; operador corrigido `f1c2266fd05342d845b485a09d1121cda75570d3`.
As fontes de produção são idênticas entre esses dois commits; a versão interna
da DLL identifica o primeiro. DLL `f0aa8c5c2e41724067052923a77cbe35cb6748c615785b468b9e913303f9ec25`,
PID 238611; um reinício controlado sem salas, zero automáticos.
1472 verificações C#, 226 HTTP, seis testes de capacidade e seis do preparador passaram.

## Consumo pelo app e piloto

Consultar `POST /v1/station/online/multiplayer/command`, ação `capabilities`,
com a autenticação e prova assinada já existentes. Conferir assinatura,
`requestId` e licença. Selecionar o perfil que corresponde **ao motor instalado**,
edição/conteúdo/core/runtime/perfil/controles e capacidade; enviar a mesma
tupla completa nos comandos de sala. Não selecionar o primeiro perfil histórico
nem reutilizar o ID R168. Os IDs exatos estão no recibo.

Capcom vs. SNK — Millennium Fight 2000 Pro: item
`station_767e5e92f2f353479e6120f1c28f96a7`, perfil
`psx-r169-20261015-005-versus`, motor
`pcsx-rearmed-ff81ed17-player-v3-cddba501a25c`, modo VS/duas vagas.
As 84 edições PSX e 157 N64 passaram nas consultas locais/públicas assinadas;
23 admissões/167 recusas PSX, 230 admissões/245 recusas N64, quatro motores e
12 modos GC/Wii conferidos. Transporte público com dois clientes PSX e quatro
N64, TLS/SPKI/prioridade, pausa/retomada e saída STATE3 antes de close1000 passou.
São clientes sintéticos; imagem, áudio, controles e saída física ainda exigem
o piloto R169 nos dois aparelhos, observando o convidado por 25 segundos.
PSX mantém 23 modos 2P em 19 edições, 17 impedidos e 53 edições individuais.
R168 não deve ser usada como cliente PSX: seu incidente NoSuchMethod Android
foi recebido e preservado; o cadastro R168 aplicado permanece histórico válido.

## Auditoria integral e continuidade

As 4.744 edições foram cruzadas sem novas aprovações: 139 com modo multiplayer
documentado por revisão, 171 locais, 18 com revisão pendente, 325 sem perfil
exato, 1.087 sem motor online no APK, 3.003 com aprovação histórica sem prova
por jogo e uma com formato incompatível. Ver o [resumo agregado](coverage-audit/coverage-summary.json).
Detalhes por edição permanecem privados. Isso não declara todo o catálogo online.
Preservados licenças reais, esquema, outros serviços, ROMs, capas, saves,
rede onboard/DNS, limites 100/800, fallback R164 e demais pendências.

**Não repetir cadastro, deploy ou provas completas R169/R168.** Nova entrada
requer baseline protegida fresca e preflight do conjunto inteiro. Rollback usa
registros, ferramentas e software compatíveis; não colocar o módulo antigo
de 8 MiB sobre os modos R169. Instalador do APK continua MANUAL.
O monitor horário existente continua responsável pelos próximos retornos.

## Histórico preservado

# R169 PSX CAS — handoff ao servidor

Leia o [handoff R169](HANDOFF-R169-PSX-CAS-20261015.md) e o [recibo do kit](server-kit-receipt-r169.json). Esta branch contém somente documentação e recibo; `serverApplied=false` para **R169** permanece até o operador executar o cadastro e devolver prova do ambiente real. A R168 já foi aplicada no [recibo de 82c81df](https://github.com/luziellacerda/Servidor-pix/blob/82c81df9c042f1470a6b94e4cede7059c9db1865/docs/station-android/r168/server-return.json).

O [kit privado exato](https://github.com/luziellacerda/TurboramaEmulationStation/tree/5158d84add5e8efd03c020cf7561f8fc100729a1/station-server-handoffs/r169) está no commit `5158d84add5e8efd03c020cf7561f8fc100729a1`, ZIP SHA-256 `859b33b156698abecadadf035f8117a4b2b680439c4fed32eb2001347dc6c832`. Não há APK no Git. Dois aparelhos têm o APK R169 assinado com hash instalado conferido e abriram o painel; gameplay PSX ainda não foi validado.

No A56 com R169, *Capcom vs. SNK — Millennium Fight 2000 Pro* exibiu “não há modo aprovado para esta edição e o motor instalado”. Não usar a R168 como cliente PSX devido à falha Android; seu cadastro já aplicado é a baseline de dados. Cadastrar R169 aditivamente com o [preparador](https://github.com/luziellacerda/Servidor-pix/blob/280539b/docs/station-android/r169/operator/README.md) e devolver recibo APLICADO com capacidades públicas autenticadas antes do piloto físico. Preservar o N64 já funcional.
