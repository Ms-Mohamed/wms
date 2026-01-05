import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ordersApi, productsApi, warehousesApi, stockApi } from '../services/api';
import { Dialog } from '@headlessui/react';
import { TruckIcon, PlusIcon } from '@heroicons/react/24/outline';
import { useToast } from '@chakra-ui/react';

export default function SalesOrders() {
    const queryClient = useQueryClient();
    const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
    const [shipModal, setShipModal] = useState<{ isOpen: boolean; orderId: number | null }>({ isOpen: false, orderId: null });
    const toast = useToast();

    // Form state for creating orders
    const [customerName, setCustomerName] = useState('');
    const [customerEmail, setCustomerEmail] = useState('');
    const [customerAddress, setCustomerAddress] = useState('');
    const [orderItems, setOrderItems] = useState<{ productId: number; warehouseId: number; quantity: number; unitPrice?: number; discount: number }[]>([]);

    // Temporary state for adding items
    const [newItemProductId, setNewItemProductId] = useState('');
    const [newItemWarehouseId, setNewItemWarehouseId] = useState('');
    const [newItemQuantity, setNewItemQuantity] = useState(1);
    const [newItemDiscount, setNewItemDiscount] = useState(0);

    // Shipment state
    const [shipmentItems, setShipmentItems] = useState<{ orderItemId: number; locationId: number; quantity: number }[]>([]);

    // Queries
    const { data: orders, isLoading } = useQuery({
        queryKey: ['orders'],
        queryFn: async () => (await ordersApi.getAll()).data,
    });

    const { data: products } = useQuery({
        queryKey: ['products'],
        queryFn: async () => (await productsApi.getAll()).data,
        enabled: isCreateModalOpen,
    });

    const { data: warehouses } = useQuery({
        queryKey: ['warehouses'],
        queryFn: async () => (await warehousesApi.getAll()).data,
        enabled: isCreateModalOpen,
    });

    const { data: stocks } = useQuery({
        queryKey: ['stocks'],
        queryFn: async () => (await stockApi.getAll()).data,
        enabled: shipModal.isOpen || isCreateModalOpen,
    });

    // Mutations
    const createMutation = useMutation({
        mutationFn: (data: any) => ordersApi.create(data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['orders'] });
            setIsCreateModalOpen(false);
            resetForm();
            toast({
                title: 'Commande créée',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Erreur',
                description: error.response?.data?.error || "Impossible de créer la commande",
                status: 'error',
                duration: 5000,
                isClosable: true,
            });
        }
    });

    const shipMutation = useMutation({
        mutationFn: ({ id, data }: { id: number; data: any }) => ordersApi.ship(id, data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['orders'] });
            queryClient.invalidateQueries({ queryKey: ['stocks'] });
            setShipModal({ isOpen: false, orderId: null });
            setShipmentItems([]);
            toast({
                title: 'Expédition confirmée',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Erreur',
                description: error.response?.data?.error || "Erreur lors de l'expédition",
                status: 'error',
                duration: 5000,
                isClosable: true,
            });
        },
    });

    const resetForm = () => {
        setCustomerName('');
        setCustomerEmail('');
        setCustomerAddress('');
        setOrderItems([]);
        setNewItemProductId('');
        setNewItemWarehouseId('');
        setNewItemQuantity(1);
        setNewItemDiscount(0);
    };

    const addItem = () => {
        if (!newItemProductId || !newItemWarehouseId) return;
        const product = products?.find(p => p.id === parseInt(newItemProductId));
        setOrderItems([...orderItems, {
            productId: parseInt(newItemProductId),
            warehouseId: parseInt(newItemWarehouseId),
            quantity: newItemQuantity,
            unitPrice: product?.unitPrice,
            discount: newItemDiscount,
        }]);
        setNewItemProductId('');
        setNewItemQuantity(1);
        setNewItemDiscount(0);
    };

    const handleCreateOrder = () => {
        createMutation.mutate({
            customerName,
            customerEmail,
            customerAddress,
            items: orderItems,
        });
    };

    const handleOpenShipModal = (orderId: number) => {
        const order = orders?.find(o => o.id === orderId);
        if (order) {
            // Initialize shipment items with order items
            setShipmentItems(order.items.map((item: any) => ({
                orderItemId: item.id,
                locationId: 0,
                quantity: item.quantity,
            })));
        }
        setShipModal({ isOpen: true, orderId });
    };

    const handleShip = () => {
        if (!shipModal.orderId) return;
        shipMutation.mutate({
            id: shipModal.orderId,
            data: { items: shipmentItems },
        });
    };

    const getAvailableLocationsForProduct = (productId: number) => {
        return stocks?.filter(s => s.productId === productId && s.availableQuantity > 0) || [];
    };

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-bold text-gray-900">Commandes Clients</h1>
                <button
                    onClick={() => setIsCreateModalOpen(true)}
                    className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center gap-2"
                >
                    <PlusIcon className="h-5 w-5" />
                    Nouvelle Commande
                </button>
            </div>

            {isLoading ? (
                <div className="text-center py-12">Chargement...</div>
            ) : (
                <div className="bg-white rounded-lg shadow overflow-hidden">
                    <table className="min-w-full divide-y divide-gray-200">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">N° Commande</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Client</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Date</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Statut</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Total</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {orders?.map((order: any) => (
                                <tr key={order.id}>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">{order.orderNumber}</td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{order.customerName}</td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        {new Date(order.orderDate).toLocaleDateString('fr-FR')}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap">
                                        <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${order.status === 'Pending' ? 'bg-yellow-100 text-yellow-800' :
                                            order.status === 'Shipped' ? 'bg-green-100 text-green-800' :
                                                'bg-gray-100 text-gray-800'
                                            }`}>
                                            {order.status}
                                        </span>
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        {order.totalAmount?.toFixed(2)} €
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm">
                                        {order.status === 'Pending' && (
                                            <button
                                                onClick={() => handleOpenShipModal(order.id)}
                                                className="text-blue-600 hover:text-blue-900 flex items-center gap-1"
                                            >
                                                <TruckIcon className="h-4 w-4" />
                                                Expédier
                                            </button>
                                        )}
                                        {['shipped', 'delivered'].includes(order.status.toLowerCase()) && (
                                            <a
                                                href={`/invoices/${order.id}`}
                                                className="text-blue-600 hover:text-blue-900 hover:underline"
                                            >
                                                Voir Facture
                                            </a>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Create Order Modal */}
            <Dialog open={isCreateModalOpen} onClose={() => setIsCreateModalOpen(false)} className="relative z-50">
                <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
                <div className="fixed inset-0 flex items-center justify-center p-4">
                    <Dialog.Panel className="w-full max-w-3xl transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                        <Dialog.Title className="text-lg font-medium leading-6 text-gray-900 mb-4">
                            Nouvelle Commande Client
                        </Dialog.Title>

                        <div className="grid grid-cols-2 gap-4 mb-4">
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Nom Client</label>
                                <input
                                    type="text"
                                    value={customerName}
                                    onChange={(e) => setCustomerName(e.target.value)}
                                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Email</label>
                                <input
                                    type="email"
                                    value={customerEmail}
                                    onChange={(e) => setCustomerEmail(e.target.value)}
                                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                />
                            </div>
                        </div>

                        <div className="mb-4">
                            <label className="block text-sm font-medium text-gray-700">Adresse</label>
                            <textarea
                                value={customerAddress}
                                onChange={(e) => setCustomerAddress(e.target.value)}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                rows={2}
                            />
                        </div>

                        <div className="border-t border-gray-200 pt-4">
                            <h4 className="text-sm font-medium text-gray-900 mb-2">Produits</h4>
                            <div className="flex gap-2 items-end mb-4 bg-gray-50 p-3 rounded">
                                <div className="flex-1">
                                    <label className="block text-xs font-medium text-gray-500">Produit</label>
                                    <select
                                        value={newItemProductId}
                                        onChange={(e) => setNewItemProductId(e.target.value)}
                                        className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1"
                                    >
                                        <option value="">Sélectionner</option>
                                        {products?.map(p => (
                                            <option key={p.id} value={p.id}>{p.name} ({p.code})</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="w-32">
                                    <label className="block text-xs font-medium text-gray-500">Entrepôt</label>
                                    <select
                                        value={newItemWarehouseId}
                                        onChange={(e) => setNewItemWarehouseId(e.target.value)}
                                        className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1"
                                    >
                                        <option value="">Sélectionner</option>
                                        {warehouses?.map(w => (
                                            <option key={w.id} value={w.id}>{w.name}</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="w-32">
                                    <label className="block text-xs font-medium text-gray-500">Qté</label>
                                    <div className="relative">
                                        <input
                                            type="number"
                                            min="1"
                                            value={newItemQuantity}
                                            onChange={(e) => {
                                                const val = parseInt(e.target.value) || 0;
                                                const stock = stocks?.find(s =>
                                                    s.productId === parseInt(newItemProductId) &&
                                                    s.warehouseId === parseInt(newItemWarehouseId)
                                                );
                                                console.log('Stock check:', { pid: newItemProductId, wid: newItemWarehouseId, stock, val, stocks });
                                                const max = stock ? (stock.availableQuantity ?? 0) : 0;
                                                setNewItemQuantity(val > max ? max : val);
                                            }}
                                            className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1 pr-14"
                                        />
                                        <div className="absolute right-1 top-1/2 -translate-y-1/2 text-[10px] text-gray-500 pointer-events-none bg-white px-1 font-semibold z-10">
                                            max: {
                                                (newItemProductId && newItemWarehouseId)
                                                    ? (stocks?.find(s =>
                                                        s.productId === parseInt(newItemProductId) &&
                                                        s.warehouseId === parseInt(newItemWarehouseId)
                                                    )?.availableQuantity ?? '...')
                                                    : '-'
                                            }
                                        </div>
                                    </div>
                                </div>
                                <button
                                    onClick={addItem}
                                    className="bg-green-600 text-white px-3 py-1 rounded hover:bg-green-700 text-sm h-8"
                                >
                                    Ajouter
                                </button>
                            </div>

                            {orderItems.length > 0 && (
                                <ul className="space-y-2 mb-4">
                                    {orderItems.map((item, idx) => {
                                        const product = products?.find(p => p.id === item.productId);
                                        const warehouse = warehouses?.find(w => w.id === item.warehouseId);
                                        return (
                                            <li key={idx} className="flex justify-between items-center bg-gray-50 p-2 rounded">
                                                <span className="text-sm">{product?.name} - {warehouse?.name} x {item.quantity}</span>
                                                <button
                                                    onClick={() => setOrderItems(orderItems.filter((_, i) => i !== idx))}
                                                    className="text-red-600 hover:text-red-900 text-sm"
                                                >
                                                    Retirer
                                                </button>
                                            </li>
                                        );
                                    })}
                                </ul>
                            )}
                        </div>

                        <div className="mt-4 flex justify-end gap-2">
                            <button
                                onClick={() => setIsCreateModalOpen(false)}
                                className="bg-gray-200 text-gray-700 px-4 py-2 rounded hover:bg-gray-300"
                            >
                                Annuler
                            </button>
                            <button
                                onClick={handleCreateOrder}
                                disabled={!customerName || orderItems.length === 0}
                                className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:bg-gray-300"
                            >
                                Créer
                            </button>
                        </div>
                    </Dialog.Panel>
                </div>
            </Dialog>

            {/* Ship Order Modal */}
            <Dialog open={shipModal.isOpen} onClose={() => setShipModal({ isOpen: false, orderId: null })} className="relative z-50">
                <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
                <div className="fixed inset-0 flex items-center justify-center p-4">
                    <Dialog.Panel className="w-full max-w-2xl transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                        <Dialog.Title className="text-lg font-medium leading-6 text-gray-900 mb-4">
                            Expédier la Commande
                        </Dialog.Title>

                        <p className="text-sm text-gray-600 mb-4">
                            Sélectionnez l'emplacement de prélèvement pour chaque produit :
                        </p>

                        <div className="space-y-4">
                            {shipmentItems.map((shipItem, idx) => {
                                const order = orders?.find(o => o.id === shipModal.orderId);
                                const orderItem = order?.items.find((i: any) => i.id === shipItem.orderItemId);
                                const availableLocations = orderItem ? getAvailableLocationsForProduct(orderItem.productId) : [];

                                return (
                                    <div key={idx} className="border border-gray-200 rounded p-3">
                                        <div className="flex justify-between items-start mb-2">
                                            <div>
                                                <p className="font-medium text-sm">{orderItem?.productName}</p>
                                                <p className="text-xs text-gray-500">Quantité: {shipItem.quantity}</p>
                                            </div>
                                        </div>
                                        <div>
                                            <label className="block text-xs font-medium text-gray-700 mb-1">
                                                Emplacement de prélèvement
                                            </label>
                                            <select
                                                value={shipItem.locationId}
                                                onChange={(e) => {
                                                    const newItems = [...shipmentItems];
                                                    newItems[idx].locationId = parseInt(e.target.value);
                                                    setShipmentItems(newItems);
                                                }}
                                                className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                            >
                                                <option value="0">Sélectionner un emplacement</option>
                                                {availableLocations.map(loc => (
                                                    <option key={loc.id} value={loc.locationId}>
                                                        {loc.locationName} (Stock: {loc.availableQuantity})
                                                    </option>
                                                ))}
                                            </select>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>

                        <div className="mt-6 flex justify-end gap-2">
                            <button
                                onClick={() => setShipModal({ isOpen: false, orderId: null })}
                                className="bg-gray-200 text-gray-700 px-4 py-2 rounded hover:bg-gray-300"
                            >
                                Annuler
                            </button>
                            <button
                                onClick={handleShip}
                                disabled={shipmentItems.some(item => item.locationId === 0)}
                                className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:bg-gray-300 flex items-center gap-2"
                            >
                                <TruckIcon className="h-5 w-5" />
                                Confirmer l'Expédition
                            </button>
                        </div>
                    </Dialog.Panel>
                </div>
            </Dialog>
        </div>
    );
}
