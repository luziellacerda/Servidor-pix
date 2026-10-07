# Entrega APP R71 completa — destinatário: operador Station Android

**APP → SERVIDOR. Código publicado e APK instalado no A56; NÃO é retorno do
servidor, publicação Linux nem homologação de partida em dois aparelhos.**

- App: branch `fix/station-online-layout-recovery-r71-20261007`.
- Commit completo: `0368bf0586fd6fa1b38ed4c98bcf6cee73810595`.
- [Fontes, receitas e recibos exatos](https://github.com/luziellacerda/TurboElden/tree/0368bf0586fd6fa1b38ed4c98bcf6cee73810595/versions/station-online-layout-r71-20261007).
- [Handoff original no app](https://github.com/luziellacerda/TurboElden/tree/0368bf0586fd6fa1b38ed4c98bcf6cee73810595/versions/station-online-layout-r71-20261007/HANDOFF-APP-R71-PARA-SERVIDOR-20261007.md).
- Base desta entrega documental: retorno servidor `6f8dcead3af84ed1c8323f6e241900bf2f36a938`.
- APK final: `556170c32b6dd25fb5084693826d854adf736b4a1df156fd9229a8458b025018`.

## Ações solicitadas ao servidor

1. Revisar a integração contra o commit do app acima, especialmente o predicado
   nativo de pausa corrigido e o encerramento do executor no callback terminal6.
2. Aplicar o plano qualificado de publicação do candidato de recuperação recebido,
   preservar clientes v1 e acrescentar os dois IDs/hash Windows desta entrega.
   As adições exatas estão em `entrega-app-r71-20261007/evidence/server-engine-registry-additions.json`.
3. Devolver recibo da API/DLL/registro/flags realmente ativos; confirmar capacidade
   assinada `station-stream.v2`, transporte `relay-wss-v2` e engines registradas.
4. Definir extensão assinada para perfis dos membros da própria sala fora da
   paginação social, conforme seção5 do handoff abaixo; sem inventar campos.

Não substituir clientes/Activities por versões antigas nem usar o hash do ELF
Linux como identidade do ELF Windows. Não modificar outros serviços por efeito
desta entrega. O operador executa a publicação com seu plano de retorno.

## Arquivos anexos e referência

`entrega-app-r71-20261007/STATUS.json` e os recibos locais de compilação,
instalação, runtime, carrossel, manifestos e testes estão copiados nesta branch.
`DELIVERY-FILES.json` relaciona seus bytes/hashes. Para caminhos de fontes e
receitas citados abaixo, a autoridade é a pasta do app no commit exato acima.
Os caminhos `entrega-app-r71-20261007/evidence/` são relativos a este documento.

Samsung A56: instalada e hash conferido; após desbloqueio, carrossel ESActivity
confirmado em RESUMED. Motorola Edge30: última conferência R70, ainda precisa R71.
O usuário fará conferências físicas. A instalação não autoriza alegar gameplay,
retomada real ou redução térmica medida. Não há APK ou mídia privada nesta branch.

---

# APP → SERVIDOR: entrega R71 conciliada, ativação coordenada e nomes dos membros

**Destinatário: operador/implementador do Servidor-pix, exclusivamente serviço
Station Android. Este é o retorno do APP ao candidato de recuperação recebido;
não é um recibo de implantação do servidor nem prova de gameplay em dois Android.**

## 1. Origem e conciliação

Retorno lido: branch `fix/station-online-recovery-r67-20261007`, commit
`6f8dcead3af84ed1c8323f6e241900bf2f36a938`, documento
`docs/station-android/RETORNO-IMPLEMENTACAO-RETOMADA-ONLINE-STATION-20261007.md`.
Código API recebido: `32ce9d2b30bb23deef17899e10fc285f38ea81ab`.
Código Android recebido: `4d30401a80658dd56666ef10f48d9556b3fdd9e9`;
entrega `65dd0aa05330ace8df0a91a89c895a3aab6428f8`.

A receita recebida exigia R68. O APK realmente instalado antes desta preparação
é R70, SHA `c12ee4e2928a629be4a2cc6dc9201fc7c5722e21a1fa8385d21da7a7dc69a32e`,
em A56 e Motorola, conforme recibos R70. A montagem R71 parte deste APK e compara
todas as entradas. Conserva seleção das coleções R70, os 57 vídeos anteriores, 30 fps somente
no menu, DEX30/BIOS, motores locais e assinatura. Acrescenta o vídeo Todos os jogos
SNES (total 58) e seu poster. Não restauramos a Activity antiga.

A composição final combina193 fontes Java R67 com o delta de recuperação e os
ajustes visuais/navegação: 198 fontes. DEX28 agora é
`1a04582a02804ecbe4b075dd487118a171b0b0f305dbb8884015bbede09b25d7`.
Mudam somente Login e dois auxiliares de navegação; os outros fontes do cliente
continuam idênticos, incluindo autorização, assinatura de prova e downloads.
O manifesto e os recibos desta pasta identificam o DEX35/runtime/APK finais;
não usar os hashes de binários Linux recebidos como identidade do build Windows.

## 2. O que muda no aplicativo

- Menu lateral Salas/Pessoas/Criar sala/Conversas; botões36dp em alvos48dp,
  ícones consistentes, ação principal/saída diferenciadas.
- Os dois participantes aparecem na própria sala e na confirmação de início.
  Nome, anfitrião/convidado, você e prontidão são vinculados por ID ao snapshot.
- Confirmação presa a instance/roomId/itemId: alteração fecha o diálogo. O start
  revalida um único snapshot e o transporte compatível v1/v2 antes de enviar.
- Delta recebido: Binder/sessão, negociação v2, ticket inicial/resume-relay,
  geração/hashes/prova, fila limitada/ACK e overlay de espera, sem Leave por
  simples perda de rede. Retorno de processo perdido informa recovery-failed.
- Navegação: ESActivity passa a singleTask; Login autorizado retoma a tarefa
  existente. Entradas do lobby são coordenadas para não multiplicar telas nem
  deslocar a partida em andamento. Novo intent conserva sala, seleção e rascunhos.
- Nenhuma mudança em endpoint, TLS/pin, licença, download, ROM, controle local ou save.

## 3. Correção adicional encontrada no candidato nativo

O candidato marcava NEED_SYNC para qualquer stall diferente de NONE.
`NETPLAY_STALL_INPUT_LATENCY` também é uma espera normal de ajuste de latência.
Essa condição podia iniciar uma barreira global indevida com a conexão saudável.

O overlay `native-recovery` mantém os patches recebidos imutáveis na pasta de
origem e restringe a condição de recuperação a `NETPLAY_STALL_RUNNING_FAST`.
Não altera algoritmos de latência, buffers, opções ou cores. A comparação das
quatro fontes geradas encontra **uma única condição diferente** em
`network/netplay/netplay_frontend.c`. A regressão falha no predicado anterior e
passa no corrigido; resultados/manifestos estão em `evidence`.

Consequência operacional: registrar os IDs/hashes do runtime corrigido desta
entrega; não os do candidato Linux nem do primeiro build Windows sem correção.

### Encerramento terminal do executor no candidato Android

O callback de falha irrecuperável envia o evento Binder 6 enquanto o painel de
saída ainda pode estar visível. O candidato recebido marcava failed, mas conservava
o heartbeat/executor. R71 cancela essa consulta e encerra o executor original.
Eventos tardios 1/2/3/5 não o reativam. O evento humano 4 posterior continua aceito
uma única vez e usa uma tarefa pontual para Leave, apenas se a mesma sala existe.
Não adicionar Leave ao evento 6: isso converteria falha em saída humana indevida.
Background normal v2 e queda temporária mantêm heartbeat e associação à sala.
Os testes exercitam 6→4, callbacks tardios, idempotência e 40 corridas reais de threads.

## 4. Ativação que falta no servidor

O handoff recebido declara produção ativa da07355 e RecoveryEnabledfalse.
O novo cliente requer engine+hashes aprovados e capacidade station-stream.v2.
**Instalar esse runtime não ativa v2 na API. Não há fallback para outro motor
com hash diferente.** Clientes antigos devem manter v1 e todos os registros antigos.

Esquema exato recebido e preservado no repositório do app:

- [CONTRATO.md recebido](https://github.com/luziellacerda/TurboElden/blob/0368bf0586fd6fa1b38ed4c98bcf6cee73810595/docs/server/recovery-r67-20261007/CONTRATO.md)
- [openapi.json recebido](https://github.com/luziellacerda/TurboElden/blob/0368bf0586fd6fa1b38ed4c98bcf6cee73810595/docs/server/recovery-r67-20261007/openapi.json)
- [contract-vectors.json recebido](https://github.com/luziellacerda/TurboElden/blob/0368bf0586fd6fa1b38ed4c98bcf6cee73810595/docs/server/recovery-r67-20261007/contract-vectors.json)

O snapshot habilitado precisa anunciar `recoveryCapabilities:["station-stream.v2"]`,
`transports` contendo `relay-wss-v2` e a engine aprovada com
`recoveryProtocol:"station-stream.v2"`, além dos hashes exatos. Os nomes acima
vêm do contrato recebido; não são campos novos propostos por este handoff.

Use o arquivo `entrega-app-r71-20261007/evidence/server-engine-registry-additions.json` correspondente ao
`entrega-app-r71-20261007/evidence/package.json` final. A receita de merge recebida deve acrescentar esses
registros ao índice efetivo sem substituir os anteriores. Preserve isolamento,
prova, usuário exclusivo, configurações e outros produtos. O operador executa a
publicação qualificada prevista no retorno; nenhum Linux foi implantado pelo app.

Devolver um **novo recibo SERVIDOR → APP**, com:

1. Fonte, serviço/ExecStart, SHA da DLL efetivamente ativa e dataUTC.
2. SHA do registro efetivo e presença dos IDs exatos desta entrega + IDs antigos.
3. RecoveryEnabled efetivo; capacidades/transporte/engines assinados por sessão.
4. Gates de prova/nonce/ticket, criação/pronto/start/resume, v1 e serviços preservados.
5. Alvo autorizado para homologar os dois APKs idênticos sem partidas em andamento.

Não reenviar apenas confirmação de push Git: o app precisa distinguir código
publicado, processo implantado, registro ativo e execução Android.

## 5. Completar nomes dos participantes em qualquer página

Achado no `StationOnline.cs` recebido: peers pagina100, enquanto room.members e
room.ready contêm IDs completos. Um membro fora da página pode não ter nickname
no snapshot. Não há memberProfiles no contrato entregue.

R71 conserva até256 nomes previamente observados por instância e nunca inventa
nomes; se nunca recebeu o perfil, mostra “Nome indisponível”. Isso evita trocar
nomes ao paginar, mas não substitui um contrato completo.

Solicitação ao servidor: definir e documentar uma extensão aditiva assinada que
garanta os perfis dos membros da **própria sala**, independentemente da paginação
social; retornar esquema, exemplos sintéticos e teste com host/convidado fora da
página. Não publicar dados privados/cadastro civil ou adicionar nomes não
contratados por adivinhação. O app incorporará o campo efetivamente acordado no
retorno, mantendo member IDs como autoridade de participação/prontidão.

## 6. Evidências e limites

Consulte `entrega-app-r71-20261007/evidence/local-tests.json`, os recibos nativos/interop/pacote e STATUS.
Passaram 1.152 verificações JVM e 56 guardas vinculadas às 198 fontes/DEX finais.
Interoperação isolada passou 46 verificações TLS/WSS/TCP e 1.620.000 bytes exatos;
os 32 vetores públicos são repetidos no interop e já estão incluídos na contagem JVM.
Provas locais exercitam JSON assinado sintético, nomes/páginas, contrato,
transporte TLS/TCP e invariantes de pausa. Nenhuma dessas provas equivale a uma
partida física SNES/Mega com imagem, som, inputs e restauração nos dois Android.

Depois de ativar o servidor e atualizar os dois aparelhos, executar a matriz
do retorno: perdas host/convidado/ambos,Wi-Fi/dados,background,espera prolongada,
saída humana, revogação, perda do processo e versões distintas. Registrar pausa
confirmada no JNI e primeiro evento correlacionado, sem tokens/tickets/capturas
pessoais. Temperatura/consumo em espera e capacidadev2 continuam sem medição física.

Não declarar estabilidade geral, recuperar processo morto, segurança absoluta ou
causa da queda antiga resolvida apenas porque o pacote compilou.

## 7. Identidades finais compiladas em Windows

- APK: `556170c32b6dd25fb5084693826d854adf736b4a1df156fd9229a8458b025018` / 2122893402 bytes.
- DEX35: `a90056f54ae31d82c67462329fa5d877677ebf2fc031edc95d9b53f6ebc933e2`.
- Runtime corrigido: `d66267cd42507388f86034deb47e9f9670875784efb9afabfb8ffc64e3f3a856` /10.668.624 bytes.
- Certificado: `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`.
- engines.json: `034e02d866a5b5849ce093ed1b71748c3237b1c9ba93660aef1a70e7891bc6ac`.
- Adições do registro: `24385de0a3d3c492a5b9543512b3d535a3fdf7ed8192f2be37549fe80ae682a4`.
- IDs liberáveis: `bsnes-mercury-performance-79d7f9de-rs2-d66267cd4250` e `clownmdemu-d43c2708-rs2-d66267cd4250`.
- Carrossel: `9671f553858ef1c98c320ea85cfa7aa29350c1a9c341a3fe08b8d46e4a8a48d7`; Manifest: `cc00ab597882646285b963cf28c7bbc25088092268450d610e35d83a7e6444c4`.
- Gate APK: seis entradas alteradas, uma adição de vídeo, 13.218 preservadas; assinatura/16KiB
  e classes sem duplicação conferidos. Nenhum APK/ROM/BIOS/chave foi enviado ao Git.

## 8. Instalação no A56 e próximo aparelho

R71 instalada no Samsung A56 em07/10/2026 às21:22:46UTC. O SHA integral do APK
no telefone coincide com o da seção7. UID/data original preservados; instalação
direta sem cópia extra, sem desinstalação, limpeza de dados ou partida interrompida.
A entrada oficial abriu LoginActivity; tela bloqueada/apagada impediu confirmar
autologin e o menu. Não afirmar gameplay ou navegação visual homologados.
Recibo sanitizado: `entrega-app-r71-20261007/evidence/installation-samsung-r71-20261007.json`.

Motorola Edge30: última conferência R70. Atualizar para esta mesma R71 antes de
homologar recuperação em dupla. A instalação no A56 não prova implantação Linux.
STATUS separa compilação, instalação, visual, gameplay e servidor.

### Observação posterior à instalação

Após o desbloqueio pelo usuário, ActivityManager confirmou ESActivity em RESUMED:
o carrossel abriu no pacote instalado. O mantenedor atribuiu o relato de Voltar
à instalação/estado antigo aberto e informou que deixou de ocorrer. Não foi
demonstrado crash da R71 nem criado um novo patch por essa hipótese. Não equivale
a teste do retorno de salas ou gameplay. Recibo sanitizado:
`entrega-app-r71-20261007/evidence/android-followup-samsung-r71.json`.
