# Integração da TurboramaStation Android no servidor compartilhado

Esta pasta é o ponto de partida para uma ferramenta ou pessoa que vá adicionar o produto TurboramaStation Android ao servidor. Ela explica o que foi observado em 30/09/2026, quais serviços já atendem clientes e como preparar a integração preservando PIX, Suite Windows, EmulationStation Windows, site e conteúdo. É um guia versionado de trabalho; não é um pacote de implantação.

Tarefa atual: [HANDOFF-APP-IMPLEMENTAR-STATION-20261002.md](HANDOFF-APP-IMPLEMENTAR-STATION-20261002.md) (APK). Preencha [RETORNO-APP-IMPLEMENTAR-STATION-20261002.md](RETORNO-APP-IMPLEMENTAR-STATION-20261002.md). A análise de segurança e Cloudflare do servidor está em [RETORNO-SEGURANCA-JOGOS-STATION-20261002.md](RETORNO-SEGURANCA-JOGOS-STATION-20261002.md).

A porta `5192` já foi executada. O retorno dessa execução está em [RETORNO-EXECUCAO-5192-20261001.md](RETORNO-EXECUCAO-5192-20261001.md).

Leia nesta ordem: [regras de trabalho](AGENTS.md), [inventário observado](INVENTARIO-SERVIDOR.md) e [plano de implementação](PLANO-INTEGRACAO.md). O inventário é um retrato datado, não uma afirmação de que uma branch Git corresponde aos binários instalados. Para atualizar a parte observável sem modificar o servidor, execute `bash scripts/inventario-somente-leitura.sh` nesta pasta.

O [retorno da implementação candidata](RETORNO-IMPLEMENTACAO-CANDIDATA-20260930.md) distingue o backend já testado em isolamento dos bloqueios para venda e implantação. O [OpenAPI candidato](openapi-candidato.yaml) documenta apenas o contrato parcial; catálogo/download ainda falham fechados.

Para iniciar outra ferramenta neste projeto, informe este diretório como pasta de trabalho e peça: “Leia AGENTS.md, README.md, INVENTARIO-SERVIDOR.md e PLANO-INTEGRACAO.md; execute apenas o inventário de leitura; identifique as diferenças atuais antes de propor código. Preserve os serviços já ativos.”

O [handoff original do Android](https://github.com/luziellacerda/TurboElden/blob/f7887438e41107e73ed32ddeaf634f998b708ded/docs/server/HANDOFF-TURBORAMASTATION-ANDROID-20260930.md) descreve a proposta de compra, ativação, sessão, painel e downloads. O [handoff operacional anterior da Suite](../../HANDOFF-TUTORIAL-COMPLETO-SERVIDOR-TURBORAMA-SUITE-20260903.md) explica o sistema existente. Ambos contêm observações históricas: confirme sempre o estado em execução antes de trabalhar.

Na data deste levantamento, PIX, API Suite, administração Suite e gateway de conteúdo estavam ativos. As verificações HTTP locais em `127.0.0.1:5187/v1/health`, `:5190/health` e `:5191/health` responderam `200`. Isso não comprova o fluxo completo de compra, licença ou jogo. O login remoto do Android ainda não está implementado no APK registrado.

Este guia está na branch de documentação do repositório privado `luziellacerda/Servidor-pix`. Não o copie para um repositório público sem revisar caminhos internos e dados operacionais. Nenhum segredo foi lido ou incluído aqui.
