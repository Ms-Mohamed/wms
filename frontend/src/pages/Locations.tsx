import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { locationsApi } from '../services/api';
import type { CreateLocationDto } from '../types';
import {
    Box,
    Button,
    Heading,
    Text,
    Table,
    Thead,
    Tbody,
    Tr,
    Th,
    Td,
    Card,
    CardBody,
    Input,
    FormControl,
    FormLabel,
    Modal,
    ModalOverlay,
    ModalContent,
    ModalHeader,
    ModalFooter,
    ModalBody,
    ModalCloseButton,
    useDisclosure,
    useToast,
    IconButton,
    Badge,
    HStack,
    VStack
} from '@chakra-ui/react';
import { PlusIcon, TrashIcon, MapPinIcon, PencilIcon } from '@heroicons/react/24/outline';

export default function Locations() {
    const { isOpen, onOpen, onClose } = useDisclosure();
    const queryClient = useQueryClient();
    const toast = useToast();

    const [newLocation, setNewLocation] = useState<CreateLocationDto>({
        code: '',
        name: '',
        zone: '',
        warehouseId: 1
    });

    const { data: locations, isLoading } = useQuery({
        queryKey: ['locations'],
        queryFn: async () => (await locationsApi.getAll()).data,
    });

    const createMutation = useMutation({
        mutationFn: (data: CreateLocationDto) => locationsApi.create(data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['locations'] });
            toast({
                title: 'Emplacement créé',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
            onClose();
            setNewLocation({ code: '', name: '', zone: '', warehouseId: 1 });
        },
        onError: () => {
            toast({
                title: 'Erreur',
                description: "Impossible de créer l'emplacement",
                status: 'error',
                duration: 3000,
                isClosable: true,
            });
        }
    });

    const deleteMutation = useMutation({
        mutationFn: (id: number) => locationsApi.delete(id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['locations'] });
            toast({
                title: 'Emplacement supprimé',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
        },
        onError: (error: any) => {
            toast({
                title: 'Erreur',
                description: error.response?.data || "Impossible de supprimer l'emplacement",
                status: 'error',
                duration: 5000,
                isClosable: true,
            });
        }
    });

    const [editingLocationId, setEditingLocationId] = useState<number | null>(null);

    const updateMutation = useMutation({
        mutationFn: ({ id, data }: { id: number; data: CreateLocationDto }) => locationsApi.update(id, data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['locations'] });
            toast({
                title: 'Emplacement mis à jour',
                status: 'success',
                duration: 3000,
                isClosable: true,
            });
            onClose();
            resetForm();
        },
        onError: () => {
            toast({
                title: 'Erreur',
                description: "Impossible de mettre à jour l'emplacement",
                status: 'error',
                duration: 3000,
                isClosable: true,
            });
        }
    });

    const resetForm = () => {
        setNewLocation({ code: '', name: '', zone: '', warehouseId: 1 });
        setEditingLocationId(null);
    };

    const handleEdit = (loc: any) => {
        setEditingLocationId(loc.id);
        setNewLocation({
            code: loc.code,
            name: loc.name,
            zone: loc.zone || '',
            warehouseId: loc.warehouseId
        });
        onOpen();
    };

    const handleClose = () => {
        resetForm();
        onClose();
    };

    const handleSubmit = () => {
        if (!newLocation.code || !newLocation.name) {
            toast({
                title: 'Erreur',
                description: "Le code et le nom sont requis",
                status: 'error',
            });
            return;
        }

        if (editingLocationId) {
            updateMutation.mutate({ id: editingLocationId, data: newLocation });
        } else {
            createMutation.mutate(newLocation);
        }
    };

    if (isLoading) return <Text>Chargement...</Text>;

    return (
        <Box maxW="7xl" mx="auto" px={{ base: 4, md: 8 }} py={6}>
            <HStack justify="space-between" mb={8}>
                <VStack align="start" spacing={1}>
                    <Heading as="h1" size="xl" color="blue.600" display="flex" alignItems="center">
                        <Box as={MapPinIcon} w={8} h={8} mr={3} />
                        Emplacements
                    </Heading>
                    <Text color="gray.500">Gérez la cartographie de votre entrepôt (Allées, Rayons, Casiers)</Text>
                </VStack>
                <Button
                    leftIcon={<Box as={PlusIcon} w={5} h={5} />}
                    colorScheme="blue"
                    onClick={onOpen}
                >
                    Nouvel Emplacement
                </Button>
            </HStack>

            <Card bg="white" shadow="md" borderRadius="xl">
                <CardBody>
                    <Table variant="simple">
                        <Thead bg="gray.50">
                            <Tr>
                                <Th>Code</Th>
                                <Th>Nom / Description</Th>
                                <Th>Zone</Th>
                                <Th>Entrepôt</Th>
                                <Th width="100px">Actions</Th>
                            </Tr>
                        </Thead>
                        <Tbody>
                            {locations?.map((loc) => (
                                <Tr key={loc.id} _hover={{ bg: "gray.50" }}>
                                    <Td>
                                        <Badge colorScheme="purple" fontSize="md">{loc.code}</Badge>
                                    </Td>
                                    <Td fontWeight="medium">{loc.name}</Td>
                                    <Td>{loc.zone || '-'}</Td>
                                    <Td>{loc.warehouseId === 1 ? 'Principal' : loc.warehouseId}</Td>
                                    <Td>
                                        <HStack spacing={2}>
                                            <IconButton
                                                aria-label="Modifier"
                                                icon={<Box as={PencilIcon} w={4} h={4} />}
                                                colorScheme="blue"
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => handleEdit(loc)}
                                            />
                                            <IconButton
                                                aria-label="Supprimer"
                                                icon={<Box as={TrashIcon} w={4} h={4} />}
                                                colorScheme="red"
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => {
                                                    if (window.confirm('Voulez-vous vraiment supprimer cet emplacement ?')) {
                                                        deleteMutation.mutate(loc.id);
                                                    }
                                                }}
                                                isLoading={deleteMutation.isPending && deleteMutation.variables === loc.id}
                                            />
                                        </HStack>
                                    </Td>
                                </Tr>
                            ))}
                            {locations?.length === 0 && (
                                <Tr>
                                    <Td colSpan={5} textAlign="center" py={8} color="gray.500">
                                        Aucun emplacement défini. Créez-en un pour commencer !
                                    </Td>
                                </Tr>
                            )}
                        </Tbody>
                    </Table>
                </CardBody>
            </Card>

            {/* Modal Creation/Edition */}
            <Modal isOpen={isOpen} onClose={handleClose}>
                <ModalOverlay />
                <ModalContent>
                    <ModalHeader>{editingLocationId ? 'Modifier l\'emplacement' : 'Nouvel Emplacement'}</ModalHeader>
                    <ModalCloseButton />
                    <ModalBody pb={6}>
                        <VStack spacing={4}>
                            <FormControl isRequired>
                                <FormLabel>Code (ex: A-01-01)</FormLabel>
                                <Input
                                    placeholder="CODE-UNIQUE"
                                    value={newLocation.code}
                                    onChange={(e) => setNewLocation({ ...newLocation, code: e.target.value.toUpperCase() })}
                                />
                                <Text fontSize="xs" color="gray.500">Utilisez un format comme Allée-Rayon-Niveau</Text>
                            </FormControl>

                            <FormControl isRequired>
                                <FormLabel>Nom</FormLabel>
                                <Input
                                    placeholder="ex: Rayon Boissons"
                                    value={newLocation.name}
                                    onChange={(e) => setNewLocation({ ...newLocation, name: e.target.value })}
                                />
                            </FormControl>

                            <FormControl>
                                <FormLabel>Zone (Optionnel)</FormLabel>
                                <Input
                                    placeholder="ex: Zone Froide"
                                    value={newLocation.zone}
                                    onChange={(e) => setNewLocation({ ...newLocation, zone: e.target.value })}
                                />
                            </FormControl>
                        </VStack>
                    </ModalBody>

                    <ModalFooter>
                        <Button variant="ghost" mr={3} onClick={handleClose}>
                            Annuler
                        </Button>
                        <Button colorScheme="blue" onClick={handleSubmit} isLoading={createMutation.isPending || updateMutation.isPending}>
                            {editingLocationId ? 'Modifier' : 'Créer'}
                        </Button>
                    </ModalFooter>
                </ModalContent>
            </Modal>
        </Box>
    );
}
