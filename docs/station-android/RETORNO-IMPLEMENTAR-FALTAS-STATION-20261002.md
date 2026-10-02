# Retorno — implementar o que ainda falta

Preencha e faça push nesta branch. Não cole pepper, DSN, token admin, chave privada, verifier, código de ativação nem URL de jogo. Não ative a licença. Não emita código.

## Licença, só leitura

- parou porque: concluiu
- licenseId conferido: `STA-D7AE45616B415B2C7550315C0392C5D8`
- enrollment_state agora: PENDING_ENROLLMENT
- activation_consumed agora: false
- segunda licença criada: nao (continua 1 linha `TURBORAMA_STATION_ANDROID`)
- código novo emitido: nao (esta rodada do handoff)
- licença ativada desta máquina: nao

## Backup

- caminho do arquivo, sem conteúdo: `/var/backups/turborama-station/STA-D7AE45616B415B2C7550315C0392C5D8-20261002.sql`
- tamanho em bytes: 6655
- modo e dono: 600 root:root
- o licenseId `STA-D7AE45616B415B2C7550315C0392C5D8` está no arquivo: sim (8 ocorrências)
- o arquivo foi commitado no Git: nao

## Rollback da 028

- rollback executado: nao
- o que a 028 alterou e não deve ser apagado: a `028_station_android` só alargou CHECKs (`product_id` e SKU passam a aceitar Station), criou `station_devices`, `station_challenges`, `station_sessions`, `station_customer_projection`, o índice `ux_station_activation_verifier` e grants. Enquanto existir a licença `STA-D7AE45616B415B2C7550315C0392C5D8`, estreitar esses CHECKs ou DROP das tabelas apaga ou quebra esta compra. Não há down migration segura com a linha Station presente.
- passos para voltar o serviço sem down migration destrutiva: 1) retirar o `include` nginx de `^~ /v1/station/` e recarregar só o nginx se um dia for preciso isolar a rota; 2) `systemctl mask --now turborama-station-api.service` (5192); 3) manter Postgres, a `028` e as linhas desta licença; 4) Suite 5190, admin.sock e PIX 5187 ficam. Não DELETE, não `028` down, não restart de 5190/PIX nesta ordem.

## Papéis

- papel `turborama-suite` existe: sim
- papel `turborama-suite-admin` existe: sim
- algum deles lê esta licença Station: sim (os dois `SELECT` devolveram `STA-D7AE45616B415B2C7550315C0392C5D8` / `TURBORAMA_STATION_ANDROID` / `ACTIVE` / `PENDING_ENROLLMENT` / não consumida)
- senha ou grant foi alterado: nao

## O que não foi aberto

- admin 5191 foi substituído: nao (gateway PID 2948; admin unix `turborama-suite-admin` PID 2665)
- cartão Android no `/admin` foi publicado: nao no SPA Suite `/admin` (esse binário não tem rota Station). O gerador humano está no TurboBox `https://turbobox.lzgames.com.br/admin/station`, helper isolado `127.0.0.1:5194`
- motivo de não publicar o cartão: publicar no SPA Suite exigiria trocar o processo/socket da 5191, o que este handoff proíbe
- site ou TurboBox foi editado: nao nesta rodada do handoff (checkout intacto)
- SKU `STATION_ANDROID_LIFETIME_1_DEVICE` existe no site: nao (fulfillment TurboBox só `SUITE_LIFETIME_1_DEVICE`; produtos visíveis: TurboBox Max ativo, Suite vitalícia inativa)
- catálogo ou download foi aberto: nao
- `GET /v1/station/artifacts/{grantId}` foi criado: nao (público HTTP 404)
- 5190 reiniciada: nao (PID 2943)
- 5192 reiniciada: nao (PID 86341)
- PIX reiniciado: nao (PID 2940)
- migration nova aplicada: nao (`028_station_android` já estava)

## Processo

- PID e hash da DLL 5190: PID 2943, `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`
- PID e hash da DLL 5192: PID 86341, `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`
- PID do admin/gateway: gateway 5191 PID 2948; admin unix PID 2665
- PID do PIX: 2940

## Gerador de senhas (já no servidor — como será usado)

O gerador humano já está no ar, isolado da Suite 5190 e do admin.sock.

- Onde o humano gera de novo: TurboBox `https://turbobox.lzgames.com.br/admin/station` (login admin, CSRF, confirmação da senha administrativa).
- Canal: helper `turborama-station-issue-admin.service` em `127.0.0.1:5194` (PID 102270, `GET /health` 200). Não abre porta pública. Não troca 5191.
- O que faz: gira o HMAC da mesma licença `STA-…`. Não ativa o aparelho. Não cria segunda licença. A senha anterior deixa de valer na hora. A senha nova aparece uma vez na tela.
- Unidade no ar: `turborama-station-issue-admin.service`. Token só em arquivo 640 no servidor. Pepper e DSN não saem deste retorno.

Como passa a ser usado depois deste retorno:

1. Na compra paga, a primeira senha vale **48 horas**. O cliente recebe no WhatsApp (número do cadastro) um texto formatado com emoji, com produto, pedido, valor, licença, senha, prazo de 48 horas e o aviso de que, se não ativar ou tiver problema, a loja gera outra senha de **30 minutos**.
2. Senhas geradas de novo pelo humano em `/admin/station` valem **30 minutos**. A anterior morre. Também podem ir no WhatsApp do cliente.
3. Quando o aparelho confirmar a senha (ativação `BOUND` / `activation_consumed`), o WhatsApp do cliente recebe a mensagem de confirmação: licença liberada neste aparelho, senha já usada, suporte pela loja.

Esta rodada do handoff não mudou TTL nem disparou WhatsApp. Backup, papéis e rollback foram só leitura + arquivo 600. TTL 48h/30min e as duas mensagens WhatsApp ficam na sequência operacional imediata, no gerador 5194 e no TurboBox, sem reiniciar 5190, 5192, 5191 nem PIX.
