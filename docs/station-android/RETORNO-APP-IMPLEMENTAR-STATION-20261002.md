# Retorno — implementação no aplicativo Turborama Station

Preencha depois das mudanças no APK. Não cole pepper, DSN, token, chave privada, senha, bearer, device id, grant, ponto de montagem nem URL de jogo. Não abra catálogo nem download no servidor.

## Medição no aparelho

- parou porque:
- APK / versão / extra `stationLogin`:
- título da abertura deixou de ser o texto fixo `BEM-VINDO DE VOLTA`: sim ou nao
- `GET /v1/station/me` roda ao abrir pelo ícone: sim ou nao
- nome visível (sem citar o valor se for dado real de cliente; na licença de teste pode citar `Teste Station`):
- 503 em `/me` preserva o último nome: sim ou nao
- senha não é pedida de novo com licença local válida: sim ou nao
- `GET /v1/station/catalog` 503 abre a biblioteca com lista salva: sim ou nao
- app guarda URL de jogo ou grant em disco: nao
- pin TLS SPKI conferido: sim ou nao
- catálogo ou download abertos no servidor por esta rodada: nao

## O que mudou no APK

- arquivos / telas:
- sessão 180 s renovada como:
- cache de perfil:
- cache de catálogo / capas:
- cliente de artifacts (desligado até o servidor abrir):

## Confirmação

- servidor 5190/5192/PIX reiniciado: nao
- senha nova emitida: nao
- licença ativada de novo: nao
- WhatsApp enviado pelo app: nao
- Cloudflare / Nginx alterados: nao
