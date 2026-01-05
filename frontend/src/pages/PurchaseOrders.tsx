import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useLocation } from 'react-router-dom';
import { suppliersApi, productsApi, purchaseOrdersApi, locationsApi } from '../services/api';
import { Dialog } from '@headlessui/react';
import type { CreatePurchaseOrderDto } from '../types';

export default function PurchaseOrders() {
    const queryClient = useQueryClient();
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [receiptModal, setReceiptModal] = useState<{ isOpen: boolean; orderId: number | null }>({ isOpen: false, orderId: null });

    // Form State
    const [selectedSupplier, setSelectedSupplier] = useState('');
    const [expectedDate, setExpectedDate] = useState('');
    const [notes, setNotes] = useState('');
    const [orderItems, setOrderItems] = useState<{ productId: number; quantity: number; unitCost: number }[]>([]);

    // Temporary state for adding a new line item
    const [newItemProductId, setNewItemProductId] = useState('');
    const [newItemQuantity, setNewItemQuantity] = useState(1);
    const [newItemCost, setNewItemCost] = useState(0);

    const location = useLocation();
    const { initialProduct } = location.state || {};

    // Queries
    const { data: orders, isLoading } = useQuery({
        queryKey: ['purchaseOrders'],
        queryFn: async () => (await purchaseOrdersApi.getAll()).data,
    });

    const { data: suppliers } = useQuery({
        queryKey: ['suppliers'],
        queryFn: async () => (await suppliersApi.getAll()).data,
        enabled: isModalOpen,
    });

    const { data: products } = useQuery({
        queryKey: ['products'],
        queryFn: async () => (await productsApi.getAll()).data,
        enabled: isModalOpen || receiptModal.isOpen,
    });

    // Effect to handle navigation from Analytics
    useEffect(() => {
        if (initialProduct) {
            setIsModalOpen(true);
            setNewItemProductId(initialProduct.id.toString());
            setNewItemCost(initialProduct.unitCost || 0);
            // Ideally we'd also pre-fill reorder quantity if available, 
            // but we'll stick to basic product selection for now.
        }
    }, [initialProduct]);

    // Effect to pre-select location based on default product location
    useEffect(() => {
        if (receiptModal.isOpen && receiptModal.orderId && orders && products) {
            const order = orders.find(o => o.id === receiptModal.orderId);
            if (order && order.items && order.items.length > 0) {
                // Use the default location of the first product found
                const firstItemProductId = order.items[0].productId;
                const product = products.find(p => p.id === firstItemProductId);
                if (product && product.defaultLocationId) {
                    setSelectedLocation(product.defaultLocationId.toString());
                }
            }
        }
    }, [receiptModal.isOpen, receiptModal.orderId, orders, products]);

    const [selectedLocation, setSelectedLocation] = useState('');

    const { data: locations } = useQuery({
        queryKey: ['locations'],
        queryFn: async () => (await locationsApi.getAll()).data,
        enabled: receiptModal.isOpen,
    });

    // Mutations
    const createMutation = useMutation({
        mutationFn: (data: CreatePurchaseOrderDto) => purchaseOrdersApi.create(data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] });
            setIsModalOpen(false);
            resetForm();
        },
    });

    const receiveMutation = useMutation({
        mutationFn: ({ id, locationId }: { id: number; locationId?: number }) => purchaseOrdersApi.receive(id, locationId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] });
            queryClient.invalidateQueries({ queryKey: ['stock'] });
            setReceiptModal({ isOpen: false, orderId: null });
            setSelectedLocation('');
        },
    });

    const resetForm = () => {
        setSelectedSupplier('');
        setExpectedDate('');
        setNotes('');
        setOrderItems([]);
        setNewItemProductId('');
        setNewItemQuantity(1);
        setNewItemCost(0);
    };

    const addItem = () => {
        if (!newItemProductId || newItemQuantity <= 0 || newItemCost < 0) return;
        setOrderItems([...orderItems, {
            productId: parseInt(newItemProductId),
            quantity: newItemQuantity,
            unitCost: newItemCost
        }]);
        setNewItemProductId('');
        setNewItemQuantity(1);
        setNewItemCost(0);
    };

    const removeItem = (index: number) => {
        setOrderItems(orderItems.filter((_, i) => i !== index));
    };

    const handleSubmit = () => {
        if (!selectedSupplier || orderItems.length === 0) {
            alert('Veuillez sélectionner un fournisseur et ajouter au moins un produit.');
            return;
        }

        const dto: CreatePurchaseOrderDto = {
            supplierId: parseInt(selectedSupplier),
            expectedDate: expectedDate ? new Date(expectedDate).toISOString() : undefined,
            notes: notes,
            items: orderItems
        };

        createMutation.mutate(dto);
    };

    const getProductName = (id: number) => products?.find(p => p.id === id)?.name || 'Inconnu';

    if (isLoading) return <div>Chargement...</div>;

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-semibold text-gray-900">Commandes Achat (Inbound)</h1>
                <button
                    onClick={() => setIsModalOpen(true)}
                    className="bg-indigo-600 text-white px-4 py-2 rounded-md hover:bg-indigo-700"
                >
                    Nouvelle Commande
                </button>
            </div>

            <div className="bg-white shadow overflow-hidden sm:rounded-md">
                <ul className="divide-y divide-gray-200">
                    {orders?.map((order) => (
                        <li key={order.id} className="px-6 py-4">
                            <div className="flex items-center justify-between">
                                <div>
                                    <div className="flex items-center">
                                        <h3 className="text-lg font-medium text-gray-900 mr-2">{order.orderNumber}</h3>
                                        <span
                                            className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full 
                        ${order.status === 'Received' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'}`}
                                        >
                                            {order.status}
                                        </span>
                                    </div>
                                    <p className="text-sm text-gray-500">Fournisseur : {order.supplier?.name}</p>
                                </div>
                                <div className="flex items-center space-x-4">
                                    <div className="text-right text-sm text-gray-500">
                                        <p>{new Date(order.orderDate).toLocaleDateString()}</p>
                                        <p className="font-semibold text-gray-900">{order.totalAmount.toFixed(2)} €</p>
                                    </div>
                                    {order.status !== 'Received' && (
                                        <button
                                            onClick={() => setReceiptModal({ isOpen: true, orderId: order.id })}
                                            className="text-indigo-600 hover:text-indigo-900 border border-indigo-600 px-3 py-1 rounded"
                                            disabled={receiveMutation.isPending}
                                        >
                                            Réceptionner
                                        </button>
                                    )}
                                </div>
                            </div>
                        </li>
                    ))}
                    {orders?.length === 0 && <p className="p-6 text-gray-500 text-center">Aucune commande trouvée.</p>}
                </ul>
            </div>

            {/* Modal de création */}
            <Dialog open={isModalOpen} onClose={() => setIsModalOpen(false)} className="relative z-50">
                <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
                <div className="fixed inset-0 flex items-center justify-center p-4">
                    <Dialog.Panel className="w-full max-w-2xl transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                        <Dialog.Title as="h3" className="text-lg font-medium leading-6 text-gray-900 mb-4">
                            Créer une commande d'achat
                        </Dialog.Title>

                        <div className="grid grid-cols-2 gap-4 mb-4">
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Fournisseur</label>
                                <select
                                    value={selectedSupplier}
                                    onChange={(e) => setSelectedSupplier(e.target.value)}
                                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                >
                                    <option value="">Sélectionner un fournisseur</option>
                                    {suppliers?.map(s => (
                                        <option key={s.id} value={s.id}>{s.name}</option>
                                    ))}
                                </select>
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700">Date Prévue</label>
                                <input
                                    type="date"
                                    value={expectedDate}
                                    onChange={(e) => setExpectedDate(e.target.value)}
                                    className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                />
                            </div>
                        </div>

                        <div className="mb-4">
                            <label className="block text-sm font-medium text-gray-700">Notes</label>
                            <textarea
                                value={notes}
                                onChange={(e) => setNotes(e.target.value)}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                                rows={2}
                            />
                        </div>

                        <div className="border-t border-gray-200 pt-4">
                            <h4 className="text-sm font-medium text-gray-900 mb-2">Ajouter des produits</h4>
                            <div className="flex gap-2 items-end mb-4 bg-gray-50 p-3 rounded">
                                <div className="flex-1">
                                    <label className="block text-xs font-medium text-gray-500">Produit</label>
                                    <select
                                        value={newItemProductId}
                                        onChange={(e) => {
                                            const pid = e.target.value;
                                            setNewItemProductId(pid);
                                            // Auto-set cost if possible
                                            const prod = products?.find(p => p.id === parseInt(pid));
                                            if (prod) setNewItemCost(prod.costPrice);
                                        }}
                                        className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1"
                                    >
                                        <option value="">Produit</option>
                                        {products?.map(p => (
                                            <option key={p.id} value={p.id}>{p.name} ({p.code})</option>
                                        ))}
                                    </select>
                                </div>
                                <div className="w-24">
                                    <label className="block text-xs font-medium text-gray-500">Qté</label>
                                    <input
                                        type="number"
                                        min="1"
                                        value={newItemQuantity}
                                        onChange={(e) => setNewItemQuantity(parseInt(e.target.value) || 0)}
                                        className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1"
                                    />
                                </div>
                                <div className="w-24">
                                    <label className="block text-xs font-medium text-gray-500">Coût U.</label>
                                    <input
                                        type="number"
                                        step="0.01"
                                        min="0"
                                        value={newItemCost}
                                        onChange={(e) => setNewItemCost(parseFloat(e.target.value) || 0)}
                                        className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-1"
                                    />
                                </div>
                                <button
                                    onClick={addItem}
                                    type="button"
                                    className="bg-green-600 text-white px-3 py-1 rounded hover:bg-green-700 text-sm h-8"
                                >
                                    Ajouter
                                </button>
                            </div>

                            <div className="overflow-y-auto max-h-40">
                                <table className="min-w-full divide-y divide-gray-200">
                                    <thead className="bg-gray-50">
                                        <tr>
                                            <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Produit</th>
                                            <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Qté</th>
                                            <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Coût</th>
                                            <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Total</th>
                                            <th className="px-3 py-2"></th>
                                        </tr>
                                    </thead>
                                    <tbody className="bg-white divide-y divide-gray-200">
                                        {orderItems.map((item, index) => (
                                            <tr key={index}>
                                                <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{getProductName(item.productId)}</td>
                                                <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{item.quantity}</td>
                                                <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{item.unitCost} €</td>
                                                <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{(item.quantity * item.unitCost).toFixed(2)} €</td>
                                                <td className="px-3 py-2 whitespace-nowrap text-right text-sm font-medium">
                                                    <button onClick={() => removeItem(index)} className="text-red-600 hover:text-red-900">Supprimer</button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        <div className="mt-6 flex justify-end space-x-3">
                            <button
                                onClick={() => setIsModalOpen(false)}
                                className="px-4 py-2 text-gray-700 bg-gray-100 rounded hover:bg-gray-200"
                            >
                                Annuler
                            </button>
                            <button
                                onClick={handleSubmit}
                                disabled={createMutation.isPending}
                                className="px-4 py-2 bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                            >
                                {createMutation.isPending ? 'Création...' : 'Créer la commande'}
                            </button>
                        </div>
                    </Dialog.Panel>
                </div>
            </Dialog>

            {/* Modal de Réception (Location Selection) */}
            <Dialog open={receiptModal.isOpen} onClose={() => setReceiptModal({ isOpen: false, orderId: null })} className="relative z-50">
                <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
                <div className="fixed inset-0 flex items-center justify-center p-4">
                    <Dialog.Panel className="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 text-left align-middle shadow-xl transition-all">
                        <Dialog.Title as="h3" className="text-lg font-medium leading-6 text-gray-900 mb-4">
                            Réceptionner la commande
                        </Dialog.Title>

                        <div className="mb-4">
                            <p className="text-sm text-gray-500 mb-4">
                                Veuillez sélectionner l'emplacement où le stock sera stocké.
                            </p>
                            <label className="block text-sm font-medium text-gray-700">Emplacement de destination</label>
                            <select
                                value={selectedLocation}
                                onChange={(e) => setSelectedLocation(e.target.value)}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm border p-2"
                            >
                                <option value="">Sélectionner un emplacement</option>
                                {locations?.map(loc => (
                                    <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
                                ))}
                            </select>
                        </div>

                        <div className="mt-6 flex justify-end space-x-3">
                            <button
                                onClick={() => setReceiptModal({ isOpen: false, orderId: null })}
                                className="px-4 py-2 text-gray-700 bg-gray-100 rounded hover:bg-gray-200"
                            >
                                Annuler
                            </button>
                            <button
                                onClick={() => {
                                    if (receiptModal.orderId && selectedLocation) {
                                        receiveMutation.mutate({
                                            id: receiptModal.orderId,
                                            locationId: parseInt(selectedLocation)
                                        });
                                    } else {
                                        alert("Veuillez choisir un emplacement");
                                    }
                                }}
                                disabled={receiveMutation.isPending || !selectedLocation}
                                className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50"
                            >
                                {receiveMutation.isPending ? 'Réception...' : 'Confirmer la réception'}
                            </button>
                        </div>
                    </Dialog.Panel>
                </div>
            </Dialog>
        </div>
    );
}
