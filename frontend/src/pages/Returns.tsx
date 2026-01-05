import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../services/api';
import { useToast } from '@chakra-ui/react';

interface ReturnOrder {
    id: number;
    returnNumber: string;
    status: string;
    order: { orderNumber: string };
    reason: string;
}

export default function Returns() {
    const queryClient = useQueryClient();
    const [isModalOpen, setIsModalOpen] = useState(false);
    const toast = useToast();

    const { data: returns, isLoading } = useQuery({
        queryKey: ['returns'],
        queryFn: async () => (await apiClient.get<ReturnOrder[]>('/returns')).data, // Need to implement GetAll in Controller first?
    });

    const receiveMutation = useMutation({
        mutationFn: (id: number) => apiClient.post(`/returns/${id}/receive`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['returns'] });
            queryClient.invalidateQueries({ queryKey: ['stock'] });
            toast({
                title: 'Retour réceptionné',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
        },
        onError: () => {
            toast({
                title: 'Erreur',
                description: "Impossible de réceptionner le retour",
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
                <h1 className="text-2xl font-semibold text-gray-900">Retours Clients (RMA)</h1>
                <button
                    onClick={() => setIsModalOpen(true)}
                    className="bg-indigo-600 text-white px-4 py-2 rounded-md hover:bg-indigo-700"
                >
                    Créer un Retour
                </button>
            </div>

            <div className="bg-white shadow overflow-hidden sm:rounded-md">
                <ul className="divide-y divide-gray-200">
                    {/* Fallback if list undefined (API not implemented fully for getAll yet) */}
                    {(!returns || returns.length === 0) && <p className="p-4 text-gray-500">Aucun retour trouvé.</p>}

                    {returns?.map((rma) => (
                        <li key={rma.id} className="px-6 py-4">
                            <div className="flex items-center justify-between">
                                <div>
                                    <h3 className="text-lg font-medium text-gray-900">{rma.returnNumber}</h3>
                                    <p className="text-sm text-gray-500">Commande : {rma.order?.orderNumber}</p>
                                    <p className="text-sm text-gray-500">Motif : {rma.reason}</p>
                                </div>
                                <div className="flex items-center space-x-4">
                                    <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full 
                      ${rma.status === 'Received' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                                        {rma.status}
                                    </span>
                                    {rma.status === 'Requested' && (
                                        <button
                                            onClick={() => receiveMutation.mutate(rma.id)}
                                            className="text-indigo-600 hover:text-indigo-900 border border-indigo-600 px-3 py-1 rounded"
                                        >
                                            Réceptionner
                                        </button>
                                    )}
                                </div>
                            </div>
                        </li>
                    ))}
                </ul>
            </div>

            {isModalOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-gray-500 bg-opacity-75">
                    <div className="bg-white p-6 rounded-lg shadow-xl w-96">
                        <h2 className="text-xl font-bold mb-4">Créer un Retour</h2>
                        <p className="text-gray-500">Sélection commande à implémenter.</p>
                        <div className="mt-6 flex justify-end">
                            <button onClick={() => setIsModalOpen(false)} className="px-4 py-2 bg-gray-100 rounded">Fermer</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
