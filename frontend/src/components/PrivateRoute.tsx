import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function PrivateRoute() {
    const { isAuthenticated } = useAuth();
    const location = useLocation();

    if (!isAuthenticated) {
        // Rediriger vers login, mais mémoriser la page demandée
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    return <Outlet />;
}
