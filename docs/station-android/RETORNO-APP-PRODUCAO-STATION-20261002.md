# Retorno — aplicativo Turborama Station em produção

Preencha depois das mudanças no APK. Não cole pepper, DSN, token, chave privada, senha, bearer, device id, grant, ponto de montagem, nome de jogo nem URL.

## Medição no aparelho

- parou porque: apk-fora-deste-servidor
- APK / versão / extra `stationLogin`: nao-aplica (fonte Java na máquina Windows; neste host não há empacote)
- título da abertura deixou de ser o texto fixo `BEM-VINDO DE VOLTA`: nao-aplica
- `GET /v1/station/me` roda ao abrir pelo ícone: nao-aplica
- nome visível (licença de teste pode citar `Teste Station`): nao-aplica
- 503 em `/me` preserva o último nome: nao-aplica
- senha não é pedida de novo com licença local válida: nao-aplica
- `GET /v1/station/catalog` devolveu `catalog/v1`: nao (sem sessão, público 401 `STATION_SESSION_INVALID`; com sessão a `5192` assina `catalog/v1`)
- quantidade de itens na lista (sem nomes): 1816 no índice do servidor (`snes` 644, `snesbr` 191, `megadrive` 887, `megadrivebr` 94)
- capas por `coverId` (bytes, sem URL, teto 5 MiB): nao-aplica no aparelho; no servidor a rota existe, teto 5 MiB, arte revista
- gamelist sem `http`: nao-aplica
- authorize/artifacts ausentes no log do login: nao-aplica
- diff do APK só `classes8.dex` (fora META-INF): nao-aplica
- authorize devolveu `grantId` sem URL: nao-aplica no aparelho; sem sessão o authorize JSON responde 401, sem `grantId` e sem URL
- artifacts entregou bytes sem `Location`: nao-aplica no aparelho; grant inexistente 404 sem `Location`
- segundo GET do mesmo grant: 404 sim ou nao: nao-aplica (nenhuma concessão criada nesta rodada)
- app guarda URL de jogo ou grant em disco: nao
- pin TLS SPKI conferido: nao-aplica nesta máquina

## O que mudou no APK

- arquivos / telas: nenhum neste servidor
- sessão 180 s renovada como: nao-aplica
- cache de perfil: nao-aplica
- cache de catálogo / capas: nao-aplica
- download (grant só memória, arquivo privado): nao-aplica

## Confirmação

- servidor 5190/5192/PIX reiniciado por esta rodada: nao
- senha nova emitida: nao
- licença ativada desta máquina: nao
- WhatsApp enviado pelo app: nao
- Cloudflare / Nginx alterados: nao

## Servidor (só leitura)

- handoff: `HANDOFF-APP-PRODUCAO-STATION-20261002.md` commit `832f840`
- `5192` PID 247645, hash `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`, `/ready/station` 200
- `5190` PID 2943, gateway 2948, PIX 2940
- prova pública sem link: retorno `32cd165` (`sem-link-direto`)
- o APK TESTE empacota na máquina Windows, Samsung `RQCY30751WY`, e preenche de novo as linhas do aparelho deste retorno
