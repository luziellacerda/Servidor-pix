# Retorno — segurança dos jogos Station

Preencha e faça push nesta branch. Não cole pepper, DSN, token, chave privada, senha, device id, ponto de montagem, lista de arquivos nem URL de jogo. Não implemente. Não reinicie. Não ative. Não emita senha.

## Medição

- parou porque: concluiu
- M.2 de cerca de 500 GB, diferente da raiz e de `/mnt/DADOS`: sim
- esse disco está montado: sim
- algum HTTP público expõe esse disco: nao
- se expõe, só o nome do server/location, sem alias e sem caminho: nao-aplica
- `5192` em loopback e Nginx só em `/v1/station/`: sim (`127.0.0.1:5192`, `location ^~ /v1/station/` em app.lzgames.com.br)
- token interno da Suite ausente na `5192`: sim
- catálogo e authorize sem sessão: 401 sim (`STATION_SESSION_INVALID`)
- artifacts inexistente: 404 sim
- gateway de conteúdo decifra o endereço só dentro do processo: sim (`ContentUrlKeyRing.Decrypt` no consumo da concessão)
- concessão Station pode ter chave própria, sem segredo Suite: sim

## O que já protege hoje

- Público só `https://app.lzgames.com.br/v1/station/`. A `5192` escuta em `127.0.0.1`. O Nginx não cacheia (`proxy_cache off`), não encaminha Cookie/Proxy, corpo máximo 8k.
- A borda já é Cloudflare: `server: cloudflare`, `cf-cache-status: DYNAMIC`, `cache-control: no-store` nas rotas Station. O conector `cloudflared` está ativo. O IP de origem do servidor não precisa aparecer no DNS.
- O aplicativo pina a SPKI TLS de `app.lzgames.com.br`. Troca de certificado na origem sem o pin quebra o app; a borda Cloudflare continua no caminho.
- Sessão Station 180 s, desafio 60 s, sem heartbeat. A chave do aparelho fica no Android Keystore. `deviceId` é o hash da chave pública. Copiar o APK não copia essa chave. Um aparelho ativo por licença.
- Sem sessão: catálogo e authorize 401. Artifacts ainda 404. Catálogo e authorize com sessão ainda 503. O HD de jogos não tem location HTTP.
- Nome do consumidor sai de `GET /v1/station/me` (`displayName` da projeção). Capa futura não autoriza jogo.

## Cloudflare contra ataque e link clonado

Cloudflare corta volume e esconde a origem. Ele **não** substitui a concessão Station. Um link copiado só morre se o servidor consumir um uso.

1. **Proxy laranja em `app.lzgames.com.br`**  
   DNS só no Cloudflare. Origem só pelo túnel. Porta 5192, 5190, 5191 e o HD de jogos fechados na WAN.

2. **Nunca cachear Station**  
   Cache Rule: Bypass em `/v1/station/*`. Sem Cache Everything. Sem R2/público com URL permanente de jogo ou capa. Capa vai na sessão, com revisão; se um dia passar pela borda, URL assinada curta ou bytes na própria `5192`.

3. **Rate limit na borda**  
   Regras por `CF-Connecting-IP` em `/v1/station/*`: teto baixo em `activations/*` e `challenges`, teto um pouco maior em `catalog` e `me`, teto curto em `downloads/authorize` e `artifacts/*`. Isso segura enumeração de `grantId` e flood. O limitador da `5192` continua.

4. **WAF, sem quebrar o APK**  
   Managed rules + OWASP na API. **Sem** JS Challenge, CAPTCHA, Turnstile ou Cloudflare Access em `/v1/station/*` (o app nativo não resolve página HTML; Access no PIX já ficou de fora da API por isso). Bot Fight só no site, não na API Station.

5. **Link clonado sem autorização**  
   O telefone **não** guarda URL de jogo. Pede `ItemId` na sessão daquele aparelho. `authorize` devolve `grantId` cifrado, prazo curto, um uso, preso a licença + device + item. `GET /v1/station/artifacts/{grantId}` consome e entrega bytes. Segundo GET, outro IP, outro aparelho ou prazo vencido: 404/401. Cloudflare vê o mesmo path e **não** revalida o uso; quem invalida é a `5192`. Sem 307 para origem permanente (esse 307 da Suite de conteúdo não se copia para a Station: o Location viraria link clonável).

6. **Authenticated Origin Pulls**  
   Só o Cloudflare fala com o Nginx. Pedido direto ao IP da máquina não serve a Station.

7. **TLS**  
   Full (strict) no túnel. Sem HTTP claro na API. O pin SPKI do app continua no certificado que o telefone vê.

8. **O que o Cloudflare não faz**  
   Não prova Keystore. Não prova licença. Não impede um grant copiado se a origem reutilizar o mesmo `grantId`. Não substitui chave AES própria da Station. Não publica o HD.

## Plano da próxima rodada

Não execute. Inclui o uso do Cloudflare acima, sem abrir catálogo nesta análise.

- resolução de `ItemId`: o telefone manda só `ItemId`. A `5192` resolve no índice da biblioteca Station, com um ou mais volumes que o operador apontar. Tamanho de disco não entra na descoberta. Jogos novos e HDs novos entram como raízes extras. O cliente nunca escolhe arquivo.
- concessão curta, um uso, endereço cifrado até a entrega: `POST /v1/station/downloads/authorize` com sessão daquele aparelho gera concessão Station própria (chave AES da Station, fora do pepper Suite, da chave de conteúdo Suite e da sessão Suite). O endereço fica cifrado em repouso. `GET /v1/station/artifacts/{grantId}` consome um uso, prazo curto, decifra só no processo e entrega os bytes. Sem 307 para origem permanente. Cloudflare Bypass cache nesse path.
- rotas que permanecem fechadas até handoff posterior: catálogo e authorize em 503; artifacts inexistente; sem host de capa Sambox.
- por que um APK copiado não baixa jogo: sem chave Keystore, sem `deviceId`, sem sessão, sem concessão. Capa não autoriza arquivo. Link copiado de `artifacts/{grantId}` cai no segundo uso.
- o que permanece intocado: `5190`, `5191`, PIX, `/mnt/DADOS`, site, SPA `/admin`, WhatsApp, migration `028`, senha e a licença de teste.
- lista e capas no login: `GET /v1/station/catalog` com sessão, corpo assinado `catalog/v1` (identificador, nome, plataforma, revisão, capa). Revisão nova baixa só capas novas. Jogo novo aparece no próximo login, sem atualizar o APK. Sem URL permanente e sem caminho de volume.

## Confirmação

- código alterado: nao
- migration aplicada: nao
- serviço reiniciado: nao
- catálogo ou download aberto: nao
- senha nova emitida: nao
- licença ativada desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao


