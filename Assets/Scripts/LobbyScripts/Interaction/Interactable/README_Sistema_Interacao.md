# Sistema de Interação / Mãos / Mochila / Moedas

## Como as peças se encaixam

```
IInteractable / NetworkInteractable      (já existia)
        │
        ├── GrabbableItem                 pegar (reaproveita RequestInteractRpc), largar, arremessar, usar
        │       ├── FlashlightItem        exemplo: "usar" liga/desliga luz
        │       ├── SpellObjectItem       exemplo: "usar" lança um feitiço/projétil
        │       └── MeleeWeaponItem       exemplo: "usar" faz um golpe
        │
        ├── CoinPickup                    "pegar" já soma no CoinPurse e desaparece
        └── TeamCoinVault                 "pegar/interagir" esvazia o CoinPurse do player pro total do time

PlayerHandsController (no prefab do player)
        ├── decide se um item cabe (1 ou 2 mãos livres)
        ├── input de Drop/Throw (Q) e Use (clique)
        └── calcula peso das mãos → chama FirstPersonController.SetCarryWeightMultiplier

PlayerInventory (mochila, no prefab do player)
        └── guarda/retira itens por slots (ItemDefinition.InventorySlotCost)

CoinPurse (no prefab do player)
        └── saquinho de moedas, capacidade máxima, separado da mochila
```

`PlayerInteractor` (o que você já tinha) **não precisa mudar nada** - ele já chama
`RequestInteractRpc` em qualquer `NetworkInteractable`, e `GrabbableItem`/`CoinPickup`/
`TeamCoinVault` são todos `NetworkInteractable`. O "olhar e apertar E" continua igual.

## O que fazer no Editor

1. **ItemDefinition**: crie um asset por tipo de item (`Sucuri/Itens/Item Definition`).
   Defina peso, se usa 1 ou 2 mãos, tamanho (slots na mochila), se é arremessável.
2. **ItemDatabase**: crie UM asset (`Sucuri/Itens/Item Database`), arraste todos os
   `ItemDefinition` pra lista. Referencie esse MESMO asset em todo `PlayerInventory`.
3. **Prefab de item**: `NetworkObject` + `Rigidbody` + `Collider` + `GrabbableItem`
   (ou uma subclasse tipo `FlashlightItem`). Registre no `NetworkPrefabsList` do
   `NetworkManager` - obrigatório pro `PlayerInventory.ServerTryRetrieve` conseguir
   instanciar/spawnar o item de volta.
4. **Prefab do player**: adicione `PlayerHandsController`, `PlayerInventory` e
   `CoinPurse`. Em `PlayerHandsController`, arraste os dois IK targets de mão
   (os mesmos usados no `TwoBoneIKConstraint`) e a referência do
   `FirstPersonController` (`_bodyController`).
5. **Moedas**: prefab com `NetworkObject` + `Collider` + `CoinPickup`. Não precisa
   de `Rigidbody` a menos que você queira física nela no chão.

## Pontos de atenção (sendo honesto sobre o que é esqueleto)

- **NetworkTransform do item**: enquanto o item tá na mão, ele fica parented no
  socket (segue por parentesco, não por sync de posição). Quando é largado/
  arremessado, ele volta a ser um `Rigidbody` livre no mundo - nesse estado ele
  precisa de um `NetworkTransform` server-authoritative (diferente do
  `ClientNetworkTransform` do seu player) pra sincronizar a física entre clientes.
  Isso não tá no C#, é configuração de componente no prefab.
- **Direção do arremesso**: o cliente manda a direção da câmera pro servidor
  (`RequestThrowRpc`). Um cliente malicioso poderia mandar qualquer vetor - pra um
  jogo cooperativo local/com amigos isso normalmente não é problema, mas se um dia
  tiver ranking competitivo vale validar/clampar no servidor.
- **Spawn/despawn de moedas e itens da mochila**: o esqueleto usa
  `Instantiate`+`Spawn`/`Despawn` direto. Se o jogo tiver MUITAS moedas trocando de
  mão o tempo todo, vale trocar por um `NetworkObjectPool` (tem exemplo oficial da
  Unity) pra evitar hiccup de GC/instantiate.
- **MeleeWeaponItem/SpellObjectItem**: só resolvem a parte de detecção/spawn. O
  `TODO` marcado é onde entra seu sistema de dano/vida/mana de verdade - não tentei
  inventar isso porque não sei como você quer estruturar combate ainda.

## Toggle Singleplayer

Todo arquivo networked tem um bloco de comentário `SINGLEPLAYER` no topo explicando
exatamente o que trocar (tirar `NetworkBehaviour`/`NetworkVariable`/RPCs por
`MonoBehaviour`/campo comum/chamada direta). A ideia é você poder manter os mesmos
scripts em um protótipo solo e só "desligar" a camada de rede quando não precisar.
