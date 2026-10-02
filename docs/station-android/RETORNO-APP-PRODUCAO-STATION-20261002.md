# Retorno — aplicativo Turborama Station em produção

Preencha depois das mudanças no APK. Não cole pepper, DSN, token, chave privada, senha, bearer, device id, grant, ponto de montagem, nome de jogo nem URL.

## Medição no aparelho

- parou porque:
- APK / versão / extra `stationLogin`:
- título da abertura deixou de ser o texto fixo `BEM-VINDO DE VOLTA`: sim ou nao
- `GET /v1/station/me` roda ao abrir pelo ícone: sim ou nao
- nome visível (licença de teste pode citar `Teste Station`):
- 503 em `/me` preserva o último nome: sim ou nao
- senha não é pedida de novo com licença local válida: sim ou nao
- `GET /v1/station/catalog` devolveu `catalog/v1`: sim ou nao
- quantidade de itens na lista (sem nomes):
- capas por `coverId` (bytes, sem URL, teto 5 MiB): sim ou nao
- gamelist sem `http`: sim ou nao
- authorize/artifacts ausentes no log do login: sim ou nao
- diff do APK só `classes8.dex` (fora META-INF): sim ou nao
- authorize devolveu `grantId` sem URL: sim ou nao
- artifacts entregou bytes sem `Location`: sim ou nao
- segundo GET do mesmo grant: 404 sim ou nao
- app guarda URL de jogo ou grant em disco: nao
- pin TLS SPKI conferido: sim ou nao

## O que mudou no APK

- arquivos / telas:
- sessão 180 s renovada como:
- cache de perfil:
- cache de catálogo / capas:
- download (grant só memória, arquivo privado):

## Confirmação

- servidor 5190/5192/PIX reiniciado por esta rodada: nao
- senha nova emitida: nao
- licença ativada desta máquina: nao
- WhatsApp enviado pelo app: nao
- Cloudflare / Nginx alterados: nao
