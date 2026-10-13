# Origem direta dos downloads Station — 13/10/2026

**Servidor aplicado e HTTPS conferido; atualização Android ainda sem assinatura ou instalação.**

Foi criado `downloads-station.lzgames.com.br`, registro A somente DNS, TTL 60,
apontando inicialmente para o IPv4 público informado pelo operador. O encaminhamento
TCP 443 do roteador chega ao servidor existente. Nenhum outro registro DNS foi alterado.

O vhost `station-downloads.nginx.conf` encaminha exclusivamente um GET canônico de
`/v1/station/artifacts/{grantId}` à API já existente em loopback. Mantém Authorization,
prova por pedido e correlação; retira cookies e desativa buffer/cache. Demais caminhos,
queries e métodos retornam 404. Não há alias de arquivos, listagem de ROMs ou mudança
do guard da origem antiga. A API, seu banco, catálogo e licenças não foram alterados
ou reiniciados; o master do Nginx também manteve seu processo após reload.

O certificado público Let's Encrypt identifica somente o hostname dedicado. Pin
SPKI SHA256: `bcd134d59fcdbb35e0343c86d31a666cc95cf158d70b27aceceefd19158d072f`.
Certificado inicial válido até 11/01/2027. A chave privada e a credencial DNS são
arquivos privados do operador, fora deste repositório e instalados sob `/etc` com
permissão restrita. O token permite DNS Edit e Zone Read somente na zona do serviço.

`station-direct-download-dns.timer` verifica o IPv4 aproximadamente a cada minuto.
O script altera apenas o conteúdo do registro A previamente identificado. Recusa
registro substituído, duplicado, nome/tipo incorreto, proxy ativado e IP não público.
Não muda o IP de túneis ou de outros sites. A propagação também depende do cache DNS.
O encaminhamento atual usa um endereço LAN atribuído por DHCP; a reserva desse
endereço no roteador não foi confirmada nesta configuração.

`station-direct-download-certificate.timer` verifica a validade duas vezes ao dia.
A renovação usa o mesmo CSR e a mesma chave para conservar o pin do app. Valida
cadeia pública, SAN, chave e composição antes de substituir os certificados; uma
falha no teste/reload restaura os arquivos anteriores. Só faz reload do Nginx.
Os desafios DNS são restritos ao hostname e apagados após validação ou timeout.

## Conferência realizada

- 14 testes de propriedade do DNS, IP inválido, limpeza ACME e rollback de renovação.
- Quatro unidades systemd verificadas; ambos os timers ativos e primeiras execuções com sucesso.
- Proxy isolado em HTTPS real: sessão/prova/correlação preservadas, cookies removidos,
  nove caminhos/métodos não autorizados recusados.
- HTTPS público com cadeia e pin conferidos, destino direto IPv4, sem Cloudflare no tráfego.
- Pedidos de artefato sem autorização recusados pela API; catálogo e outros caminhos bloqueados.
- Guard antigo preservado no acesso direto ao domínio principal; login público existente HTTP 200.
- Nenhum desafio ACME desta configuração ficou no DNS após emissão.

Os grants conservam o contrato da API: validade de 60 segundos para iniciar,
consumo único, vínculos de licença/aparelho/sessão e prova quando exigida. Não repetir
um GET já consumido em outra origem; obter autorização nova após falha de transferência.

## Pendências do Android

O APK R139 atualmente instalado continua utilizando a origem antiga. O overlay
`integration/station-direct-download-20261013` no Git do app seleciona esta origem
e seu pin somente para o GET de artefato; login/catálogo/capas/comandos preservam a
origem e o pin atuais. Candidata unsigned preparada, mantendo 39 DEX e 266 classes
da partição alterada. Assinatura original, instalação preservando dados e teste físico
do download/velocidade ainda dependem da chave do operador. Não promover binário
unsigned, misturar identidades ou presumir ganho de velocidade com os testes HTTP.

As quatro identidades PSX/N64/GameCube/Wii precisam renovar seus vínculos de bundle
porque DEX28 faz parte do fingerprint; cores, opções e controladores permanecem iguais.
O registro administrativo v3 dessas novas identidades ainda não foi realizado.
Preservar todos os registros atuais e qualificar o APK final antes desse procedimento.

## Operação

As unidades e scripts usam apenas `/etc/station-direct-download` e
`/opt/station-direct-download`. `deploy.py` exige autenticação administrativa do
Linux, entrada privada com hashes conferidos e destinos novos; recusa substituir
configurações existentes. O pacote concreto de entrada e o recibo de implantação
ficam privados. A reversão remove somente o vhost/unidades/diretórios deste serviço,
com `nginx -t` e reload, preservando sites e API existentes.

Executar os testes sem privilégios:

```sh
python3 -B -m unittest test_cloudflare_dns.py test_certificate_renewal.py
systemd-analyze verify station-direct-download-*.service station-direct-download-*.timer
```
