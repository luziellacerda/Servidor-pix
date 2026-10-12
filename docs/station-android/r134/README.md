# TurboStations R134 partidas online e handoff do servidor

Esta entrega reúne os requisitos do aplicativo R134, a revisão das partidas com três jogadores e os incidentes de saída relatados pelo proprietário. A referência Android instalada é `4f9039eb9ce13907ce043870bfe1f6e294888556`, APK SHA256 `f8cb09184f41bc7c942ce84541dbd90d575b7240467ce0033d35be65e57caee2`. O código do servidor continua baseado em `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861`, com a entrega documental R133 preservada.

## Ordem de leitura

1. [Handoff R134 de ativação e kit](HANDOFF-APP-R134-PARA-SERVIDOR-20261011.md).
2. [Incidente de três jogadores e saída](INCIDENTE-TRES-JOGADORES-E-SAIDA.md).
3. [Dossiê completo do aplicativo](https://github.com/luziellacerda/TurboElden/tree/audit/station-consumer-online-r134-20261011/docs/handoff-completo-r134-20261011).
4. [Matriz por edição e modo](catalog/README.md) e [evidências públicas](evidence/).
5. [Revisão das verificações de compilação no GitHub](CI-DELIVERY-REVIEW.json).

O diagnóstico novo acrescenta `barrierDiagnostics` somente à visão da própria sala dos participantes. Ele mostra contagens/flags/offsets e razões de espera, sem tickets, credenciais, IDs pessoais ou bytes de jogo. A regra de início, ACK real, recuperação, autorização e saída humana permanece preservada. Os testes e a fonte estão na revisão; a publicação de Git não instala esse código em produção.

## Confirmação operacional requerida

- Confirmar os seis binários e recibos originais do kit privado R134 por hash; documentos sanitizados não são inputs do registrador.
- Usar os baselines ativos atuais para mesclar os modos e engines com autenticação administrativa nativa e transação existente.
- Confirmar os quatro IDs finais do APK instalado e os modos por edição, conteúdo, perfil e quantidade exata de jogadores.
- Registrar em uma partida de três os dois links e quatro attachments, com HELLO/PAUSED/READY do epoch atual e Pending zero.
- Confirmar Leave para host/convidado e snapshot fresco mostrando a ausência da sala antiga; registrar a categoria 409 se geração antiga.
- Devolver uma resposta única com matriz de aprovados/recusados, recibos de aplicação, prova assinada fresca e estado de publicação de capas.

O relato ainda não identifica jogo/plataforma, APK do terceiro aparelho ou trace Android. Os defeitos reproduzidos em teste local são evidência de código; não provam qual caminho produziu o diálogo no celular. Nenhum telefone foi acessado por USB nesta revisão e nenhum serviço produtivo foi implantado ou reiniciado.
