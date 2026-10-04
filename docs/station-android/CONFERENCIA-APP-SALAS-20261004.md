# CLIENTE → OPERADOR: salas instaladas no Android, 04/10/2026

Isto é evidência do aplicativo, **não retorno ou implantação do servidor**. O mantenedor respondeu que o operador aplicará o handoff do Git.

## Referências fixas

- Código servidor: `308fda4124439110a34db9beba689c6f96535b39`, branch `feat/station-online-direct-20261004`. Código não mudou nesta atualização documental.
- Handoff técnico com rotas, parâmetros e ativação: [HANDOFF-APP-PARA-SERVIDOR-ONLINE-20261004.md](HANDOFF-APP-PARA-SERVIDOR-ONLINE-20261004.md).
- Código do cliente: `dd6aff172761f40c9b8eea2d3e2c1c34a0c79034`.
- Evidência de aparelho: [TurboElden commit0cdc1f3](https://github.com/luziellacerda/TurboElden/tree/0cdc1f3e4b8473fdbbd3d1b207b60e157b19baf6/versions/station-online-20261004).
- APK efetivamente instalado/hash conferido: `827723436ac618d3b1745a873813c7781ff10e043abc6033de416c01d774703d`. Atualização -r, sem desinstalação/limpeza.

## Resultado real no telefone

Botão nativo Jogar online abriu StationRoomsActivity. Controles de apelido/criar/reconectar/voltar, jogadores/salas e campo de chat apareceram. Criação fica desativada até o servidor devolver estado válido. Voltar retornou ao catálogo sem pedir login. O usuário temporário é apenas apelido; não foi inserido jogador fictício no serviço.

A requisição real recebeu **HTTP404**, correlação **060358fe191f4bd0af58b6ada9d45d75**, e mostrou “O serviço de salas ainda não está disponível neste servidor.” Em 13:56:46 UTC, POST sem credencial nas duas rotas `/v1/station/online/command` e `/v1/station/online/events` também retornou404. Isso confirma indisponibilidade pública; não determina sozinho se a causa é proxy ou DLL antiga. Não acusar banco, licença ou catálogo sem inventário.

Para encerrar: seguir ONL-01 a ONL-07 do handoff, identificar serviço/DLL/proxy reais, publicar somente no alvo autorizado, registrar flag/motores, comprovar respostas autenticadas e enviar RETORNO-SERVIDOR-ONLINE-STATION-20261004.md. Não criar nova rota arbitrária e não apontar o APK para outro host. Criar sala, presença de dois clientes, convite, entrega de chat e partida ainda aguardam essa etapa.

## Pacote preparado no PC

.NET8, DLL portátil framework-dependent, SourceRevisionId308fda4124439110a34db9beba689c6f96535b39. `dotnet publish -c Release -p:UseAppHost=false`; não depende de executável Windows. Hash DLL `3f55002aa639253689e6421e14fc0f273cb9acec0bf7109b19aa7879974b3656`.
Arquivo local `E:\StationNetplayWork\server\StationOnline-308fda41-homologacao.zip`, SHA256 `dc81111b87d6661c63661aa1038ed329a47ae69f104de2ffef9f0701e6985892`, 812400 bytes. Não foi enviado ao servidor nem contém configuração de produção/segredos. O operador pode reconstruir do commit em seu ambiente; a publicação local não comprova execução Linux.

Teste do pacote completo em loopback, sem banco: /health200 e ambas as rotas online503 STATION_ONLINE_DISABLED com flagfalse, comportamento esperado. Os testes com função habilitada/identidade sintética continuam39 domínio+37HTTP. Não habilitar por simples troca de flag antes de identificar o serviço certo e preparar o registro de motores com hashes exatos.

## Observação independente

As células de vídeo das plataformas permanecem azuis no R7, conforme já havia sido observado no R4. Capas de jogos aparecem. Isso é pendência visual do cliente e não é explicado pelo404 das salas. Não alterar catálogo/capas/licenças para tentar corrigir esse visual.
