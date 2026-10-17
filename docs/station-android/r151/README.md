# Cadastro R151 aplicado; APK atual informado é R152

O kit R151 já foi recebido, verificado e aplicado. O cadastro contém66 modos N64/33edições, nove modos GameCube e três Wii. As respostas assinadas locais/públicas conferiram perfis exatos dos três sistemas. [Retorno aplicado](server-return.json). O relato de bloqueio abaixo ocorreu antes do cadastro e foi conservado. A produção agora informa R152 instalada no A56; [o cadastro R152 também foi concluído](../r152/server-return.json). Atualizar a consulta de capabilities do app e manter os participantes na mesma revisão. Nenhuma partida física Android foi homologada por estes recibos.

## Histórico anterior preservado

# R151 no A56: capa SNES aprovada, sala N64 bloqueada

Estado em 14/10/2026. A fonte R151 está congelada no commit
`d5afd35f57b5765a54aa6c5c751c4ede55f404bf`. O APK assinado
`c6090d6fc31dea07cdc6b6494acf4af39835ef9ec38ad1dbcdf7ebdba9b4eda5`
foi instalado no Galaxy A56 por USB. O mantenedor confirmou que a transição
entre capas SNES e o movimento das oito lacunas funcionam. O pré-processamento
do campo SNES foi observado concluindo no mesmo quadro em três trocas, sem novo
efeito, textura, temporizador ou desenho por quadro. Isso é teste visual, não
teste de partida ou medição prolongada de desempenho.

O kit privado **correspondente ao APK R151** tem SHA-256
`76cf298a4a9efffc068bddb94e312dacc10e0ca35aca2ffc57ddf8c7b8f51651`.
Inclui quatro identidades independentes, seis ELF, vinte dependências e a
proveniência dos mesmos 78 modos revisados: 66 N64, nove GameCube e três Wii.
O mantenedor informou que enviou o kit ao servidor e solicitará o handoff.
Ainda não recebemos recibo de aplicação nem resposta assinada atual para R151.
O arquivo privado ZIP, APK, capturas, logs e identificadores de aparelho ficam
fora do Git.

## Bloqueio observado ao criar sala

Na tela **Criar sala** para **1080 Snowboarding**, o indicador estava “Online”,
mas o cliente mostrou: “Não há modo aprovado para esta edição e o motor
instalado. Atualize o aplicativo e o catálogo nos participantes.” O mantenedor
relatou o mesmo bloqueio para todos os jogos N64 que tentou. A captura privada
foi conferida; SHA-256 da imagem
`f05ebab61de3a7f2db6fde60ca4b637b19933b6fc36fc8abe9e3e3575ee469f1`.

O [último cadastro aplicado documentado](../r149/server-return.json), R149 às
13h59 UTC, mantém o servidor ativo com 66 modos N64 em 33 edições. Ele contém
um modo assinado de corrida para dois para a edição
`station_c13a5bbc179ccbe871697999c62ba13d`, com motor N64
`mupen64plus-ae-33bf7021-n64-v3-dcad075758e5` e runtime SHA-256
`dcad075758e57caea6d94557318d03c7f834801419fa00a576f857ecaaaad5d0`.
O APK R151 usa motor `mupen64plus-ae-33bf7021-n64-v3-8eda486330ca` e
runtime SHA-256
`8eda486330caf7245fb733820c11b94f3ec2c2d216e8ab7ae1dd36afbed02616`.
O core N64 continua com o mesmo SHA-256
`fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8`.

O cliente exige correspondência exata de item, motor, core e runtime para
mostrar escolhas aprovadas. Assim, o cadastro R149 conhecido não autoriza a
identidade R151, embora o serviço esteja online e o jogo tenha modo aprovado
para o motor antigo. Esta divergência explica o bloqueio observado. O estado
produtivo vivo precisa ser confirmado pelo handoff do servidor antes de
afirmarmos que nenhum registro R151 foi aplicado por outro caminho.

## Conferência solicitada ao servidor

1. Conferir SHA do ZIP privado e do APK R151, além das quatro identidades,
   seis ELF, vinte dependências e 78 decisões revisadas.
2. Ler baseline protegido **fresco** de catálogo, engines, perfis e modos.
   Preparar e registrar aditivamente as identidades R151 pelo operador vigente
   sob `scan.lock`, preservando os registros e recusas anteriores. Não
   reaplicar R141/R149 nem ampliar aprovações PS1 HLE.
3. Devolver recibo de aplicação e capacidade `station-multiplayer.v3`
   assinada para a edição exata de 1080 Snowboarding, com engineId e hashes
   R151, perfil aprovado e dois jogadores permitidos. Confirmar também as
   contagens N64/GC/Wii efetivamente adicionadas.
4. Atualizar a consulta no A56 e testar criar/entrar na sala com aparelhos na
   mesma revisão. Cadastro e resposta assinada não substituem teste físico de
   jogo, controles e saída.

O kit é um registro de identidades e modos; este handoff não executa deploy,
reinício, migração, cadastro genérico ou promoção da R151 a estável.
