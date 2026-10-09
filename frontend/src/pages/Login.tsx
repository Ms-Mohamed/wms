import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Box, Button, Flex, FormControl, FormErrorMessage, FormLabel, Heading, Input, Text, VStack } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { apiClient } from '../services/api';

export default function Login() {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = location.state?.from?.pathname || '/';

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const res = await apiClient.post('/auth/login', { username, password });
      login(res.data.token);
      navigate(from, { replace: true });
    } catch {
      setError(t('auth.invalid'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Flex minH="100vh">
      <Flex flex="1" display={{ base: 'none', lg: 'flex' }} bg="ink.900" color="white" direction="column" justify="space-between" p={12}>
        <Flex align="center" gap={2.5}>
          <Flex w={8} h={8} borderRadius="md" bg="brand.500" align="center" justify="center" fontWeight={800} fontSize="sm">W</Flex>
          <Text fontWeight={700}>WMS</Text>
        </Flex>
        <Box maxW="440px">
          <Heading size="xl" letterSpacing="-0.03em" lineHeight={1.1}>{t('auth.pitchTitle')}</Heading>
          <Text mt={4} color="ink.300">{t('auth.pitchBody')}</Text>
        </Box>
        <Text fontSize="xs" color="ink.400">PostgreSQL · ASP.NET Core · FastAPI · React</Text>
      </Flex>
      <Flex flex="1" align="center" justify="center" p={6} bg="white">
        <Box as="form" onSubmit={submit} w="100%" maxW="360px">
          <Heading size="md" letterSpacing="-0.02em">{t('auth.title')}</Heading>
          <Text mt={1} mb={7} color="ink.500" fontSize="sm">{t('auth.subtitle')}</Text>
          <VStack spacing={4} align="stretch">
            <FormControl isRequired>
              <FormLabel>{t('auth.username')}</FormLabel>
              <Input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus />
            </FormControl>
            <FormControl isRequired isInvalid={!!error}>
              <FormLabel>{t('auth.password')}</FormLabel>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
              <FormErrorMessage>{error}</FormErrorMessage>
            </FormControl>
            <Button type="submit" size="lg" isLoading={busy} mt={2}>{t('auth.submit')}</Button>
          </VStack>
        </Box>
      </Flex>
    </Flex>
  );
}
