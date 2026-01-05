import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../services/api';
import { useToast } from '@chakra-ui/react';

interface Supplier {
    id: number;
    name: string;
    contactName: string;
    email: string;
    phone: string;
    address: string;
}

export default function Suppliers() {
    const queryClient = useQueryClient();
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [formData, setFormData] = useState<Partial<Supplier>>({});
    const toast = useToast();

    const { data: suppliers, isLoading } = useQuery({
        queryKey: ['suppliers'],
        queryFn: async () => {
            const response = await apiClient.get<Supplier[]>('/suppliers');
            return response.data;
        },
    });

    const createMutation = useMutation({
        mutationFn: (newSupplier: Partial<Supplier>) => apiClient.post('/suppliers', newSupplier),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['suppliers'] });
            setIsModalOpen(false);
            setFormData({});
            toast({
                title: 'Fournisseur ajouté',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
        },
        onError: () => {
            toast({
                title: 'Erreur',
                description: "Impossible d'ajouter le fournisseur",
                status: 'error',
                duration: 3000,
                isClosable: true,
            });
        }
    });

    if (isLoading) return <div>Chargement...</div>;

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-semibold text-gray-900">Fournisseurs</h1>
                <button
                    onClick={() => setIsModalOpen(true)}
                    className="bg-indigo-600 text-white px-4 py-2 rounded-md hover:bg-indigo-700"
                >
                    Nouveau Fournisseur
                </button>
            </div>

            <div className="bg-white shadow overflow-hidden sm:rounded-md">
                <ul className="divide-y divide-gray-200">
                    {suppliers?.map((supplier) => (
                        <li key={supplier.id} className="px-6 py-4">
                            <div className="flex items-center justify-between">
                                <div>
                                    <h3 className="text-lg font-medium text-gray-900">{supplier.name}</h3>
                                    <p className="text-sm text-gray-500">{supplier.contactName}</p>
                                </div>
                                <div className="text-right text-sm text-gray-500">
                                    <p>{supplier.email}</p>
                                    <p>{supplier.phone}</p>
                                </div>
                            </div>
                        </li>
                    ))}
                </ul>
            </div>

            {/* Modal Simplifiée */}
            {isModalOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-gray-500 bg-opacity-75">
                    <div className="bg-white p-6 rounded-lg shadow-xl w-96">
                        <h2 className="text-xl font-bold mb-4">Ajouter un fournisseur</h2>
                        <div className="space-y-4">
                            <input
                                className="w-full border p-2 rounded"
                                placeholder="Nom"
                                value={formData.name || ''}
                                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                            />
                            <input
                                className="w-full border p-2 rounded"
                                placeholder="Contact"
                                value={formData.contactName || ''}
                                onChange={(e) => setFormData({ ...formData, contactName: e.target.value })}
                            />
                            <input
                                className="w-full border p-2 rounded"
                                placeholder="Email"
                                value={formData.email || ''}
                                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                            />
                            <input
                                className="w-full border p-2 rounded"
                                placeholder="Téléphone"
                                value={formData.phone || ''}
                                onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                            />
                        </div>
                        <div className="mt-6 flex justify-end space-x-3">
                            <button
                                onClick={() => setIsModalOpen(false)}
                                className="px-4 py-2 text-gray-700 bg-gray-100 rounded hover:bg-gray-200"
                            >
                                Annuler
                            </button>
                            <button
                                onClick={() => createMutation.mutate(formData)}
                                className="px-4 py-2 text-white bg-indigo-600 rounded hover:bg-indigo-700"
                            >
                                Enregistrer
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
