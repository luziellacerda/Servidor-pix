# Navegação e recursos: constatação no aparelho e correção R71

## O que foi observado

Em 07/10/2026, a leitura de ActivityManager no Samsung encontrou duas tarefas
da mesma instalação: ESActivity como raiz de uma tarefa e StationRoomsActivity
como raiz de outra. O Manifest instalado declara ESActivity `singleInstance` e
Salas `standard`. O Android isola uma Activity `singleInstance` e coloca as
Activities que ela abre em outra tarefa. Isso explica as duas janelas; os
registros não demonstram duas instâncias simultâneas de ESActivity.

O SHA integral do APK lido diretamente no aparelho confirmou a R70:
`c12ee4e2928a629be4a2cc6dc9201fc7c5722e21a1fa8385d21da7a7dc69a32e`.
Os dumps brutos ficam apenas no diretório privado de trabalho em E:.

## Correção na composição R71

- O único atributo alterado no Manifest é o modo de ESActivity:
  `singleInstance` → `singleTask`. Recursos, componentes e permissões restantes
  são comparados com a base exata.
- Depois das verificações existentes de licença e armazenamento, a entrada
  reaproveita a tarefa existente. Retomar pelo ícone não deve limpar as telas
  acima do carrossel nem encerrar uma partida.
- As entradas internas das salas compartilham a política de navegação. Chamadas
  repetidas são coordenadas e uma partida existente mantém sua tarefa no topo.
- `onNewIntent` reaproveita a instância; `onStart` conserva a política existente
  de reconexão.
  Mudança de jogo é aceita somente quando não existe participação/lançamento
  em andamento; conversas continuam usando o tratamento de rascunhos existente.
- Nenhuma correção fecha todos os processos, limpa dados ou desinstala o app.

As receitas, a comparação do Manifest e os testes de navegação identificam os
arquivos efetivamente compilados. Não usar este documento como recibo de teste
físico: confira STATUS e os recibos posteriores de instalação.

## Processamento: o que a auditoria permite afirmar

O arquivo `evidence/activity-resource-audit.json` vincula as fontes aos DEX
5, 6 e 9 da R70. Vídeos e música possuem pausa/liberação ao esconder o menu;
ESActivity comunica a mudança a StationFrontend e SDL. As salas cancelam
consultas/capas ao parar e pausam animações ao perder visibilidade/foco.

Manter a tela anterior na pilha, uma imagem em cache ou um processo ocioso não
prova que ele esteja renderizando. Não foi demonstrada renderização dupla nem
medida uma redução percentual de CPU, GPU ou temperatura nesta auditoria.
Downloads pedidos pelo usuário e a manutenção da sessão online têm finalidade
própria e não são encerrados por esse ajuste de navegação.

A revisão também identificou trabalho evitável nos painéis internos ocultos do
lobby e um temporizador de recuperação v2. Esses achados estão separados no
recibo; não são apresentados como a causa comprovada do comportamento da R70.

Referência: [tarefas, pilha e modos de abertura no Android](https://developer.android.com/guide/components/activities/tasks-and-back-stack).
