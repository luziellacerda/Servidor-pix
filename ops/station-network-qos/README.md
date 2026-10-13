# Divisão automática da banda do Station

Aplicada em 13/10/2026. O controle cobre a saída **deste PC**, incluindo tráfego
local e para a internet. A API Station e o master Nginx conservaram seus processos.
Licenças, banco de produção, catálogo, motores, opções de emulação e APK não foram
alterados pela configuração de rede.

## Capacidade e empréstimo

O operador informou plano de 800 Mbps de download e aproximadamente 100 Mbps de
upload. A interface suporta gigabit, mas o enlace estava negociado a **100 Mbps**.
As medições deram 92,034 Mbps de download e 36,940 Mbps de upload no Ookla, e
aproximadamente 29 Mbps de envio no destino Cloudflare. São resultados desses
testes, não comprovação da capacidade contratada em todos os caminhos.

O teto inicial do envio para a internet é **33 Mbps**; a saída da placa tem teto
de 95 Mbps. Destinos locais possuem ramo próprio e podem aproveitar a capacidade
ociosa até 95 Mbps, sem receber o teto de internet. As reservas em cada ramo são:

| Classe | Reserva mínima | Prioridade | Uso da capacidade ociosa |
| --- | --- | --- | --- |
| Partidas diretas, túneis existentes e ACKs pequenos | 20% | Alta | Permitido |
| Demais serviços | 20% | Média | Permitido |
| Downloads HTTPS | 60% | Baixa | Permitido |

No ramo internet, isso corresponde a 6,6 / 6,6 / 19,8 Mbps. Um download sozinho
pode aproveitar o teto inteiro de 33 Mbps; a reserva passa a ser utilizada pelas
outras classes conforme há tráfego. O controle considera a **soma dos fluxos**.
As classes têm FQ-CoDel para reduzir filas; o ramo de internet permanece limitado
à capacidade inicialmente calibrada mesmo quando não há partida.

Os túneis existentes transportam vários serviços criptografados na mesma conexão.
Eles compartilham a classe interativa. A separação específica de uma partida
direta usa o par IP/porta da conexão TLS, conferido contra o socket do Nginx. O
serviço de prioridade não recebe códigos, tickets, provas, cookies ou chaves.
Uma tentativa recusada encerra o HTTP keepalive, impedindo reutilização da conexão
marcada para baixar um artefato. A autorização continua na API, também quando o
serviço auxiliar de prioridade falha.

## Rota direta das partidas

```text
wss://downloads-station.lzgames.com.br/v1/station/online/multiplayer/relay
Sec-WebSocket-Protocol: station-stream.v3
Authorization: StationRelay <ticket novo>
X-Station-Request-Proof: <prova vinculada ao ticket e ao caminho canônico>
SPKI SHA256: bcd134d59fcdbb35e0343c86d31a666cc95cf158d70b27aceceefd19158d072f
```

A inclusão dessa rota é deliberada, preservando o GET de artefato. O certificado,
DNS e encaminhamento 443 existentes foram reutilizados. Demais APIs e diagnósticos
continuam bloqueados nesse host. `X-Station-Qos: 1` indica o cadastro da fila,
sem substituir qualquer verificação do ticket. O protocolo, replay, READY/ACK,
geração, janelas e limites da API permanecem iguais.

**A R141 e a estável R143 ainda usam a origem anterior para multiplayer.** A produção do APK deve
selecionar este URI e o pin correspondente somente na conexão multiplayer v3,
preservando origem/pin de login e comandos e a rota anterior do relay legado.
Modificar somente o URI, mantendo a fábrica TLS antiga, causaria erro de pin.
Usar a estável R143 como base, mantendo os parsers/seleção multitap atuais.
O detalhe de integração está no handoff existente. Os callbacks e os ajustes de
opções da R140 devem ser conservados; qualquer alteração de fingerprint deve
seguir a renovação normal dos owners e o kit privado.

## Operação e reversão

Arquivos: `/opt/station-network-qos`, `/etc/station-network-qos/config.json`,
duas inclusões específicas do Nginx e quatro unidades `station-network-*`.
O serviço principal é persistente no boot. Um timer confere a propriedade das
filas a cada 30 segundos e recupera a política apenas sobre a fila padrão conhecida.
Uma fila de outro administrador é recusada. A tabela nftables própria apenas
classifica tráfego; não altera regras de firewall, NAT ou roteamento existentes.

Consultar: `python3 /opt/station-network-qos/policy.py status`. Ajustar as taxas
no JSON protegido e recarregar `station-network-qos.service` preserva sockets e
filas existentes. A nova taxa deve vir de medições, especialmente após corrigir
o enlace de 100 Mbps. Não forçar gigabit sem conferir cabo e porta.

Parar as unidades de prioridade/timer e depois `station-network-qos.service`
retira apenas esta classificação e devolve a fila padrão do kernel. O instalador
também restaura automaticamente o vhost se qualquer gate falhar. Não reaplicar
`deploy.py` quando o recibo privado `applied.json` já existir.

## Evidência

O recibo público fica no mesmo handoff, em
`docs/station-android/network-qos/server-return.json`. Inclui oito testes de
segurança da configuração, TLS/WebSocket real em fixture, quatro cenários de
filas Linux isoladas e duas conexões WSS com tickets/provas reais e licenças
sintéticas removidas. São provas de rede e admissão; gameplay Android ainda
depende do teste físico com a atualização do app.

Referências: [HTB](https://man7.org/linux/man-pages/man8/tc-htb.8.html),
[FQ-CoDel](https://www.rfc-editor.org/rfc/rfc8290.html),
[proxy WebSocket Nginx](https://nginx.org/en/docs/http/websocket.html).
