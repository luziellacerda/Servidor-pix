# Qualificação Java e integração de transporte R77

Esta pasta contém testes reproduzíveis do contrato, coordenação de partidas e transporte para 2, 3 e 4 participantes. Nenhum desses resultados é uma homologação de gameplay Android ou de latência na Internet.

## Resultados atuais

| Receita | Código exercitado | Resultado |
|---|---|---:|
| `run_java_contract_tests.py` | Profile, Wire, Session e RoomStartState completos; túnel controlado | 1.612 verificações |
| `run_tunnel_concurrency_tests.py` | Tunnel e Wire completos; fronteira WebSocket controlada | 148 verificações, zero callback sob o lock, zero falha causada por Remote obsoleto |
| `run_snapshot_merge_tests.py` | Três métodos de merge/revisão extraídos literalmente de OnlineClient | 90 verificações |
| `run_multiplayer_transport_tests.py`, execução 04 | Java real, biblioteca WebSocket real e endpoints C# reais em HTTPS/WSS local | 3 sessões, 117 verificações, 8.520.000 bytes TCP exatos |
| Mesma receita, execução 05 | Ordem 4/3/2 repetida duas vezes | 6 sessões, 234 verificações, 17.040.000 bytes TCP exatos |

Os recibos em `evidence/` incluem hashes de fontes, receitas, dependências e limites. As nove sessões cruzadas totalizam **351 verificações e 25.560.000 bytes**. Não somar esses bytes a tráfego de partidas físicas.

## O que o cruzamento real cobre

- Comandos autenticados e prova de requisição, respostas assinadas, criação/entrada/pronto/início e saída humana vinculada à geração.
- `N−1` canais independentes, tickets por identidade/papel/slot e associação da porta TCP de origem ao slot assinado. Os convidados e links iniciam em ordem invertida para não confundir ordem de chegada com controle.
- Tráfego exato nos dois sentidos de todos os canais, incluindo payload maior que a janela de crédito, ACK e drenagem das filas limitadas.
- Fechamento real do socket WSS no anfitrião e no convidado, nova autenticação/ticket, preservação dos sockets TCP locais e tráfego durante pausa.
- Barreira da sala: um ACK nativo retido mantém todos pausados; nenhum participante retoma cedo. Após liberação, todos alcançam a mesma época.
- Segundo plano/retorno e alternância rápida de visibilidade do anfitrião em todos os links.

O certificado, chaves e usuários são gerados exclusivamente para a fixture local. As propriedades privadas sintéticas são removidas ao terminar. Os pins, endpoints públicos e dados dos telefones permanecem intactos. A prontidão do emulador e seus sockets são simulados; nenhuma ROM/core/Activity Android é executada nesta prova.

## Falhas encontradas e tratadas

1. **Inversão de locks:** o pump chamava a sessão enquanto segurava o lock do túnel, enquanto a sessão consultava disponibilidade do túnel sob seu próprio lock. A decisão de falha é tomada sob o lock, mas o callback ocorre fora dele. A prova anterior registrou 32 callbacks sob o lock.
2. **Callbacks obsoletos:** erro tardio de Remote antigo podia encerrar uma conexão nova. A falha agora compara a identidade do Remote sob o lock. A prova anterior registrou 24 encerramentos indevidos.
3. **Estado sem época sobrescrevendo coordenação:** um callback `state(waiting)` atrasado podia apagar o estado `running` após PLAYING. O estado de execução fica nos callbacks de controle com época; disponibilidade continua impedindo execução quando o transporte está ausente.
4. **Retorno do segundo plano:** o servidor v3 preserva suspensão e exige `FOREGROUND` (12). A primeira prova cruzada desse fluxo parou esperando após autenticar novamente. O escritor único envia uma vez por Remote autenticado, antes de PAUSED/READY, respeitando a visibilidade atual.
5. **Prontidão de outro link apagada:** DATA novo invalidava READY de todos os links no C#, mas apenas o par recebia DATA/ACCEPTED para recalcular. A execução com 3 participantes reproduziu espera sem progresso. O C# candidato agora invalida só o par quando Accepted realmente avança; replay duplicado não apaga READY. A barreira ainda exige todos os links e janelas drenados. A execução 04 e as seis repetições passaram após essa correção.

Um timeout inicial ao tentar observar uma pausa curta por polling era limitação da fixture. A versão atual retém explicitamente o ACK nativo antes da desconexão para produzir uma pausa observável e testar a barreira; esse primeiro timeout não foi atribuído ao produto.

## Fontes congeladas dos testes de transporte

- Tunnel: `c117815045ad93c6736d4618cf71c3e4226faace620054acecfd64e7eb393961`.
- Session: `e019e6699e5a5a5c5f2ec63e6d334fa6bcaad7c5e5672a001480294f8ff5a4dc`.
- Wire: `318e6daa0f0ce483c9c7458ee33f918085ad4f1d9c1f2e4319895b3df7dba3c0`.
- Manifesto C# recebido: `dcb854b48241524e3beeb0478aacc4e3173116da37a6c141bc544610c66cbbb7`.

O recibo cruzado lista cada arquivo C# efetivamente copiado e compilado. Uma fonte alterada depois desses hashes precisa de nova conferência; estes testes não declaram implantação no servidor público.

## Reprodução

Usar Java 17, .NET 8 e caminhos de dependências registrados nas receitas, com saída em E:. As três receitas puras aceitam `--work`. O teste cruzado exige um diretório ainda inexistente:

```powershell
python -X utf8 -B run_multiplayer_transport_tests.py --output 'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\transport-new-run' --players 2 3 4
```

As receitas mantêm logs de laboratório em E:; somente recibos de métricas e hashes entram no snapshot. Nenhum usuário, licença, token, certificado privado ou conteúdo de jogo é necessário para a reprodução.
